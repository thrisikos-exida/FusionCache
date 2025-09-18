# 📢 FusionCache Backplane - Azure Queue + Table

This package implements a FusionCache backplane using Azure Storage Queues and Tables.

## 🚀 Quick Start

Install the package:

```PowerShell
PM> Install-Package ZiggyCreatures.FusionCache.Backplane.AzureQueueTable
```

Configure and use:

```csharp
// Manual setup
var backplane = new AzureQueueTableBackplane(new AzureQueueTableBackplaneOptions() {
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

The backplane uses Azure Storage Queues for message distribution and Azure Tables for coordination.

| Option | Description | Default |
|--------|-------------|---------|
| `ConnectionString` | Azure Storage connection string | Required |
| `QueueName` | Queue name for messages | `fusioncache-backplane` |
| `TableName` | Table name for coordination | `fusioncachebackplane` |
| `PollingInterval` | Queue polling interval | `1000ms` |

## 🔧 How it works

- **Publishing**: Messages are sent to an Azure Storage Queue
- **Subscribing**: Continuously polls the queue for new messages  
- **Coordination**: Uses Azure Tables to track active subscribers
- **Reliability**: Built-in retry and error handling