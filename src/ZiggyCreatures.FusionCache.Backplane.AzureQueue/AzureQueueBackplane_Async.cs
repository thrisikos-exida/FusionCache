using Azure;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureQueue;

public partial class AzureQueueBackplane
{
	private async ValueTask EnsureConnectionAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		if (_queueClient is not null)
			return;

		await _connectionLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			if (_queueClient is not null)
				return;

			EnsureClients();

			// Create queue if it doesn't exist
			if (_queueClient is not null)
			{
				await _queueClient.CreateIfNotExistsAsync(cancellationToken: token).ConfigureAwait(false);
			}

			// Notify connection established
			var tmp = _connectHandlerAsync;
			if (tmp is not null)
			{
				await tmp(new BackplaneConnectionInfo(false)).ConfigureAwait(false);
			}
		}
		finally
		{
			_connectionLock.Release();
		}

		if (_queueClient is null)
			throw new NullReferenceException("A connection to Azure Storage Queue is not available");
	}

	/// <inheritdoc/>
	public async ValueTask SubscribeAsync(BackplaneSubscriptionOptions subscriptionOptions)
	{
		if (subscriptionOptions is null)
			throw new ArgumentNullException(nameof(subscriptionOptions));

		if (subscriptionOptions.ChannelName is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.ChannelName cannot be null");

		if (subscriptionOptions.IncomingMessageHandler is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.IncomingMessageHandler cannot be null");

		if (subscriptionOptions.ConnectHandler is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.ConnectHandler cannot be null");

		if (subscriptionOptions.IncomingMessageHandlerAsync is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.IncomingMessageHandlerAsync cannot be null");

		if (subscriptionOptions.ConnectHandlerAsync is null)
			throw new NullReferenceException("The BackplaneSubscriptionOptions.ConnectHandlerAsync cannot be null");

		_subscriptionOptions = subscriptionOptions;
		_channelName = _subscriptionOptions.ChannelName;

		_incomingMessageHandler = _subscriptionOptions.IncomingMessageHandler;
		_connectHandler = _subscriptionOptions.ConnectHandler;
		_incomingMessageHandlerAsync = _subscriptionOptions.IncomingMessageHandlerAsync;
		_connectHandlerAsync = _subscriptionOptions.ConnectHandlerAsync;

		// CONNECTION
		await EnsureConnectionAsync().ConfigureAwait(false);

		// Start polling for messages
		StartPolling();
	}

	/// <inheritdoc/>
	public async ValueTask UnsubscribeAsync()
	{
		_ = Task.Run(() =>
		{
			_incomingMessageHandler = null;
			_incomingMessageHandlerAsync = null;
			_subscriptionOptions = null;

			Disconnect();
		});

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		// CONNECTION
		await EnsureConnectionAsync(token).ConfigureAwait(false);

		if (_queueClient is null)
			throw new InvalidOperationException("Queue client is not available");

		var serializedMessage = SerializeMessage(message);

		token.ThrowIfCancellationRequested();

		try
		{
			await _queueClient.SendMessageAsync(serializedMessage, cancellationToken: token).ConfigureAwait(false);
		}
		catch (RequestFailedException ex)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, ex, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] error publishing message", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);
			throw;
		}
	}
}