using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureQueueTable;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class AzureQueueTableBackplaneExtensions
{
	/// <summary>
	/// Adds an Azure Queue based implementation of a backplane to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{AzureQueueTableBackplaneOptions}"/> to configure the provided <see cref="AzureQueueTableBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheAzureQueueTableBackplane(this IServiceCollection services, Action<AzureQueueTableBackplaneOptions>? setupOptionsAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		if (setupOptionsAction is not null)
			services.Configure(setupOptionsAction);

		services.TryAddTransient<AzureQueueTableBackplane>();
		services.TryAddTransient<IFusionCacheBackplane, AzureQueueTableBackplane>();

		return services;
	}

	/// <summary>
	/// Adds an Azure Queue based implementation of a backplane to the <see cref="IFusionCacheBuilder" />.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the backplane to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{AzureQueueTableBackplaneOptions}"/> to configure the provided <see cref="AzureQueueTableBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithAzureQueueTableBackplane(this IFusionCacheBuilder builder, Action<AzureQueueTableBackplaneOptions>? setupOptionsAction = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithBackplane(sp =>
			{
				var options = sp.GetService<IOptionsMonitor<AzureQueueTableBackplaneOptions>>()?.Get(builder.CacheName);

				if (options is null)
					throw new InvalidOperationException($"Unable to find a valid {nameof(AzureQueueTableBackplaneOptions)} instance for the current cache name '{builder.CacheName}'.");

				setupOptionsAction?.Invoke(options);

				var logger = sp.GetService<ILogger<AzureQueueTableBackplane>>();

				return new AzureQueueTableBackplane(options, logger);
			})
		;
	}
}