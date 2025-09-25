# 📢 FusionCache Backplane - Azure Queue

This package implements a FusionCache backplane using Azure Storage Queues.

## 🚀 Quick Start

Install the package:

```PowerShell
PM> Install-Package ZiggyCreatures.FusionCache.Backplane.AzureQueue
```

Configure and use:

```csharp
// Manual setup
var backplane = new AzureQueueBackplane(new AzureQueueBackplaneOptions() {
    ConnectionString = "UseDevelopmentStorage=true" // or your Azure Storage connection string
});

cache.SetupBackplane(backplane);

// With DI
services.AddFusionCache()
    .WithAzureQueueTableBackplane(options => {
        options.ConnectionString = "UseDevelopmentStorage=true";
    });
```

## ⚙️ Configuration

The backplane uses Azure Storage Queues for message distribution between cache nodes.

| Option | Description | Default |
|--------|-------------|---------|
| `ConnectionString` | Azure Storage connection string | Required |
| `QueueName` | Queue name for messages | `fusioncache-backplane` |
| `PollingInterval` | Queue polling interval | `1000ms` |
| `MaxMessagesPerPoll` | Max messages per polling cycle | `32` |
| `MessageVisibilityTimeout` | Message visibility timeout | `30s` |
| `OperationTimeout` | Timeout for operations | `30s` |

## 🔧 How it works

- **Publishing**: Messages are sent to an Azure Storage Queue
- **Subscribing**: Continuously polls the queue for new messages  
- **Reliability**: Built-in retry and error handling with automatic message cleanup

## 📝 Full Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureQueue;

// Setup DI container
var services = new ServiceCollection();

// Add FusionCache with Azure backplane
services.AddFusionCache()
    .WithAzureQueueBackplane(options => {
        options.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey";
        options.QueueName = "my-cache-backplane";
        options.PollingInterval = TimeSpan.FromSeconds(2);
    });

var serviceProvider = services.BuildServiceProvider();
var cache = serviceProvider.GetRequiredService<IFusionCache>();

// Use cache normally - backplane will handle synchronization
await cache.SetAsync("key", "value");
var value = await cache.GetOrSetAsync("other-key", async _ => "computed-value");
```