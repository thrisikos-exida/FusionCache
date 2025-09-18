using Microsoft.Extensions.Options;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureQueueTable;

/// <summary>
/// Represents the options available for the Azure Queue + Table backplane.
/// </summary>
public class AzureQueueTableBackplaneOptions
	: IOptions<AzureQueueTableBackplaneOptions>
{
	/// <summary>
	/// The connection string used to connect to Azure Storage.
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// The name of the Azure Storage Queue to use for messages.
	/// </summary>
	public string QueueName { get; set; } = "fusioncache-backplane";

	/// <summary>
	/// The name of the Azure Table to use for coordination.
	/// </summary>
	public string TableName { get; set; } = "fusioncachebackplane";

	/// <summary>
	/// The interval at which to poll the queue for new messages.
	/// </summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(1000);

	/// <summary>
	/// The timeout for queue operations.
	/// </summary>
	public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// The maximum number of messages to retrieve in a single poll.
	/// </summary>
	public int MaxMessagesPerPoll { get; set; } = 32;

	/// <summary>
	/// The message visibility timeout (how long a message stays invisible after being received).
	/// </summary>
	public TimeSpan MessageVisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

	AzureQueueTableBackplaneOptions IOptions<AzureQueueTableBackplaneOptions>.Value
	{
		get { return this; }
	}
}