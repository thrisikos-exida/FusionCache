using Azure;
using Microsoft.Extensions.Logging;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureQueueTable;

public partial class AzureQueueTableBackplane
{
	private void EnsureConnection(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		if (_queueClient is not null && _tableClient is not null)
			return;

		_connectionLock.Wait(token);
		try
		{
			if (_queueClient is not null && _tableClient is not null)
				return;

			EnsureClients();

			// Create queue and table if they don't exist
			if (_queueClient is not null)
			{
				_queueClient.CreateIfNotExists(cancellationToken: token);
			}

			if (_tableClient is not null)
			{
				_tableClient.CreateIfNotExists(token);
			}

			// Notify connection established
			var tmp = _connectHandler;
			if (tmp is not null)
			{
				tmp(new BackplaneConnectionInfo(false));
			}
		}
		finally
		{
			_connectionLock.Release();
		}

		if (_queueClient is null)
			throw new NullReferenceException("A connection to Azure Storage Queue is not available");

		if (_tableClient is null)
			throw new NullReferenceException("A connection to Azure Storage Table is not available");
	}

	/// <inheritdoc/>
	public void Subscribe(BackplaneSubscriptionOptions subscriptionOptions)
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
		EnsureConnection();

		// Start polling for messages
		StartPolling();
	}

	/// <inheritdoc/>
	public void Unsubscribe()
	{
		_ = Task.Run(() =>
		{
			_incomingMessageHandler = null;
			_incomingMessageHandlerAsync = null;
			_subscriptionOptions = null;

			Disconnect();
		});
	}

	/// <inheritdoc/>
	public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default)
	{
		// CONNECTION
		EnsureConnection(token);

		if (_queueClient is null)
			throw new InvalidOperationException("Queue client is not available");

		var serializedMessage = SerializeMessage(message);

		token.ThrowIfCancellationRequested();

		try
		{
			_queueClient.SendMessage(serializedMessage, cancellationToken: token);
		}
		catch (RequestFailedException ex)
		{
			if (_logger?.IsEnabled(LogLevel.Error) ?? false)
				_logger.Log(LogLevel.Error, ex, "FUSION [N={CacheName} I={CacheInstanceId}]: [BP] error publishing message", _subscriptionOptions?.CacheName, _subscriptionOptions?.CacheInstanceId);
			throw;
		}
	}
}