using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureQueueTable;

/// <summary>
/// An Azure Storage Queue and Table based implementation of a FusionCache backplane.
/// </summary>
public partial class AzureQueueTableBackplane
	: IFusionCacheBackplane
{
	private readonly AzureQueueTableBackplaneOptions _options;
	private BackplaneSubscriptionOptions? _subscriptionOptions;
	private readonly ILogger? _logger;

	private readonly SemaphoreSlim _connectionLock;
	private QueueClient? _queueClient;
	private TableClient? _tableClient;

	private string? _channelName = null;
	private CancellationTokenSource? _pollingCancellationTokenSource;
	private Task? _pollingTask;

	private Action<BackplaneConnectionInfo>? _connectHandler;
	private Action<BackplaneMessage>? _incomingMessageHandler;
	private Func<BackplaneConnectionInfo, ValueTask>? _connectHandlerAsync;
	private Func<BackplaneMessage, ValueTask>? _incomingMessageHandlerAsync;

	/// <summary>
	/// Initializes a new instance of the AzureQueueTableBackplane class.
	/// </summary>
	/// <param name="optionsAccessor">The set of options to use with this instance of the backplane.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	public AzureQueueTableBackplane(IOptions<AzureQueueTableBackplaneOptions> optionsAccessor, ILogger<AzureQueueTableBackplane>? logger = null)
	{
		if (optionsAccessor is null)
			throw new ArgumentNullException(nameof(optionsAccessor));

		// OPTIONS
		_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

		// LOGGING
		if (logger is NullLogger<AzureQueueTableBackplane>)
		{
			// IGNORE NULL LOGGER (FOR BETTER PERF)
			_logger = null;
		}
		else
		{
			_logger = logger;
		}

		_connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);
	}

	/// <summary>
	/// Alternative constructor that accepts options directly.
	/// </summary>
	/// <param name="options">The options to use with this instance of the backplane.</param>
	/// <param name="logger">The <see cref="ILogger{TCategoryName}"/> instance to use. If null, logging will be completely disabled.</param>
	public AzureQueueTableBackplane(AzureQueueTableBackplaneOptions options, ILogger<AzureQueueTableBackplane>? logger = null)
		: this(Options.Create(options), logger)
	{
	}

	private void ValidateOptions()
	{
		if (string.IsNullOrWhiteSpace(_options.ConnectionString))
			throw new InvalidOperationException("Unable to connect to Azure Storage: no ConnectionString has been specified");

		if (string.IsNullOrWhiteSpace(_options.QueueName))
			throw new InvalidOperationException("QueueName cannot be null or empty");

		if (string.IsNullOrWhiteSpace(_options.TableName))
			throw new InvalidOperationException("TableName cannot be null or empty");
	}

	private void EnsureClients()
	{
		ValidateOptions();

		if (_queueClient is null)
		{
			_queueClient = new QueueClient(_options.ConnectionString, _options.QueueName);
		}

		if (_tableClient is null)
		{
			_tableClient = new TableClient(_options.ConnectionString, _options.TableName);
		}
	}

	private void Disconnect()
	{
		_connectHandler = null;
		_connectHandlerAsync = null;

		StopPolling();

		_queueClient = null;
		_tableClient = null;
	}

	private void StartPolling()
	{
		if (_pollingTask is not null && !_pollingTask.IsCompleted)
			return;

		_pollingCancellationTokenSource = new CancellationTokenSource();
		_pollingTask = Task.Run(async () => await PollForMessagesAsync(_pollingCancellationTokenSource.Token).ConfigureAwait(false));
	}

	private void StopPolling()
	{
		_pollingCancellationTokenSource?.Cancel();
		_pollingTask?.Wait(TimeSpan.FromSeconds(5));
		_pollingCancellationTokenSource?.Dispose();
		_pollingTask?.Dispose();
		_pollingCancellationTokenSource = null;
		_pollingTask = null;
	}

	private async Task PollForMessagesAsync(CancellationToken cancellationToken)
	{
		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] started polling for messages", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				if (_queueClient is null)
					break;

				var messages = await _queueClient.ReceiveMessagesAsync(_options.MaxMessagesPerPoll, _options.MessageVisibilityTimeout, cancellationToken).ConfigureAwait(false);

				foreach (var message in messages.Value)
				{
					try
					{
						var backplaneMessage = DeserializeMessage(message.MessageText);
						if (backplaneMessage is not null)
						{
							_ = Task.Run(async () =>
							{
								await OnMessageAsync(backplaneMessage).ConfigureAwait(false);
							}, cancellationToken);
						}

						// Delete the message from the queue
						await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
							_logger.Log(LogLevel.Warning, ex, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] error processing message", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);

						// Delete the problematic message to avoid infinite loop
						try
						{
							await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken).ConfigureAwait(false);
						}
						catch
						{
							// Ignore deletion errors
						}
					}
				}

				await Task.Delay(_options.PollingInterval, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
				break;
			}
			catch (Exception ex)
			{
				if (_logger?.IsEnabled(LogLevel.Error) ?? false)
					_logger.Log(LogLevel.Error, ex, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] error during polling", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);

				// Wait before retrying
				try
				{
					await Task.Delay(_options.PollingInterval, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
			_logger.Log(LogLevel.Debug, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] stopped polling for messages", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);
	}

	private string SerializeMessage(BackplaneMessage message)
	{
		var data = BackplaneMessage.ToByteArray(message);
		return Convert.ToBase64String(data);
	}

	private BackplaneMessage? DeserializeMessage(string messageText)
	{
		try
		{
			var data = Convert.FromBase64String(messageText);
			return BackplaneMessage.FromByteArray(data);
		}
		catch (Exception ex)
		{
			if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
				_logger.Log(LogLevel.Warning, ex, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] error deserializing message", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);
			return null;
		}
	}

	internal async ValueTask OnMessageAsync(BackplaneMessage message)
	{
		var tmp = _incomingMessageHandlerAsync;
		if (tmp is null)
		{
			if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
				_logger.Log(LogLevel.Trace, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] incoming message handler was null", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);
			return;
		}

		await tmp(message).ConfigureAwait(false);
	}
}