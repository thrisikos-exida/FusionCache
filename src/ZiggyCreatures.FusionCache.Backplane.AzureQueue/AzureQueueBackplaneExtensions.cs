using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureQueue;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up FusionCache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class AzureQueueBackplaneExtensions
{
	/// <summary>
	/// Adds an Azure Queue based implementation of a backplane to the <see cref="IServiceCollection" />.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{AzureQueueBackplaneOptions}"/> to configure the provided <see cref="AzureQueueBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFusionCacheAzureQueueBackplane(this IServiceCollection services, Action<AzureQueueBackplaneOptions>? setupOptionsAction = null)
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddOptions();

		if (setupOptionsAction is not null)
			services.Configure(setupOptionsAction);

		services.TryAddTransient<AzureQueueBackplane>();
		services.TryAddTransient<IFusionCacheBackplane, AzureQueueBackplane>();

		return services;
	}

	/// <summary>
	/// Adds an Azure Queue based implementation of a backplane to the <see cref="IFusionCacheBuilder" />.
	/// </summary>
	/// <param name="builder">The <see cref="IFusionCacheBuilder" /> to add the backplane to.</param>
	/// <param name="setupOptionsAction">The <see cref="Action{AzureQueueBackplaneOptions}"/> to configure the provided <see cref="AzureQueueBackplaneOptions"/>.</param>
	/// <returns>The <see cref="IFusionCacheBuilder"/> so that additional calls can be chained.</returns>
	public static IFusionCacheBuilder WithAzureQueueBackplane(this IFusionCacheBuilder builder, Action<AzureQueueBackplaneOptions>? setupOptionsAction = null)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		return builder
			.WithBackplane(sp =>
			{
				var options = sp.GetService<IOptionsMonitor<AzureQueueBackplaneOptions>>()?.Get(builder.CacheName);

				if (options is null)
					throw new InvalidOperationException($"Unable to find a valid {nameof(AzureQueueBackplaneOptions)} instance for the current cache name '{builder.CacheName}'.");

				setupOptionsAction?.Invoke(options);

				var logger = sp.GetService<ILogger<AzureQueueBackplane>>();

				return new AzureQueueBackplane(options, logger);
			})
		;
	}
}