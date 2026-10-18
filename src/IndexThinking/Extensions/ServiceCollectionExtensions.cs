using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Continuation;
using IndexThinking.Core;
using IndexThinking.Diagnostics;
using IndexThinking.Modifiers;
using IndexThinking.Stores;
using IndexThinking.Tokenization;

namespace IndexThinking.Extensions;

/// <summary>
/// Extension methods for registering IndexThinking services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds an in-memory thinking state store with no expiration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingInMemoryStorage(this IServiceCollection services)
    {
        return services.AddIndexThinkingInMemoryStorage(new InMemoryStateStoreOptions());
    }

    /// <summary>
    /// Adds an in-memory thinking state store with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Configuration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingInMemoryStorage(
        this IServiceCollection services,
        InMemoryStateStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IThinkingStateStore, InMemoryThinkingStateStore>();

        return services;
    }

    /// <summary>
    /// Adds an in-memory thinking state store with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Options configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingInMemoryStorage(
        this IServiceCollection services,
        Action<InMemoryStateStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new InMemoryStateStoreOptions();
        configure(options);

        return services.AddIndexThinkingInMemoryStorage(options);
    }

    /// <summary>
    /// Adds IndexThinking turn management services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Continuation and context settings live on <see cref="Client.ThinkingChatClientOptions"/>, the options of the client
    /// that runs the turn. (An <c>AgentOptions</c> registered here used to accept them too, and nothing read it.)
    /// </remarks>
    public static IServiceCollection AddIndexThinkingAgents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register tokenization and truncation detection services.
        // Single ApproximateTokenCounter instance serves both the neutral core interface
        // and the M.E.AI chat-message extension interface.
        services.TryAddSingleton<ApproximateTokenCounter>();
        services.TryAddSingleton<ITokenCounter>(sp => sp.GetRequiredService<ApproximateTokenCounter>());
        services.TryAddSingleton<IChatMessageTokenCounter>(sp => sp.GetRequiredService<ApproximateTokenCounter>());
        services.TryAddSingleton<ITruncationDetector, TruncationDetector>();

        // Register core agent services
        services.TryAddSingleton<IComplexityEstimator, HeuristicComplexityEstimator>();
        services.TryAddSingleton<IBudgetTracker, DefaultBudgetTracker>();
        services.TryAddSingleton<IContinuationHandler, DefaultContinuationHandler>();
        services.TryAddSingleton<IThinkingTurnManager, DefaultThinkingTurnManager>();

        return services;
    }

    // ========================================
    // Distributed Cache Storage (v0.10.0)
    // ========================================

    /// <summary>
    /// Adds a distributed cache-based thinking state store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Requires an <see cref="IDistributedCache"/> to be registered.
    /// Common registrations include:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>AddStackExchangeRedisCache()</c> - Redis backend</item>
    ///   <item><c>AddDistributedSqlServerCache()</c> - SQL Server backend</item>
    ///   <item><c>AddDistributedMemoryCache()</c> - In-memory (development only)</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddIndexThinkingDistributedStorage(this IServiceCollection services)
    {
        return services.AddIndexThinkingDistributedStorage(new DistributedCacheStateStoreOptions());
    }

    /// <summary>
    /// Adds a distributed cache-based thinking state store with specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Configuration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingDistributedStorage(
        this IServiceCollection services,
        DistributedCacheStateStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IThinkingStateStore>(sp =>
        {
            var cache = sp.GetRequiredService<IDistributedCache>();
            var opts = sp.GetRequiredService<DistributedCacheStateStoreOptions>();
            return new DistributedCacheThinkingStateStore(cache, opts);
        });

        return services;
    }

    /// <summary>
    /// Adds a distributed cache-based thinking state store with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Options configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingDistributedStorage(
        this IServiceCollection services,
        Action<DistributedCacheStateStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DistributedCacheStateStoreOptions();
        configure(options);

        return services.AddIndexThinkingDistributedStorage(options);
    }

    // ========================================
    // Health Checks (v0.10.0)
    // ========================================

    /// <summary>
    /// Adds a health check for the thinking state store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers <see cref="ThinkingStateStoreHealthCheck"/> as a scoped service.
    /// Use with ASP.NET Core health checks:
    /// </para>
    /// <code>
    /// services.AddIndexThinkingHealthChecks();
    /// services.AddHealthChecks()
    ///     .AddCheck&lt;ThinkingStateStoreHealthCheck&gt;("thinking-state-store");
    /// </code>
    /// </remarks>
    public static IServiceCollection AddIndexThinkingHealthChecks(this IServiceCollection services)
    {
        return services.AddIndexThinkingHealthChecks(new ThinkingStateStoreHealthCheckOptions());
    }

    /// <summary>
    /// Adds a health check for the thinking state store with specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Health check options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingHealthChecks(
        this IServiceCollection services,
        ThinkingStateStoreHealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton(options);
        services.TryAddScoped<ThinkingStateStoreHealthCheck>();

        return services;
    }

    /// <summary>
    /// Adds a health check for the thinking state store with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Options configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingHealthChecks(
        this IServiceCollection services,
        Action<ThinkingStateStoreHealthCheckOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ThinkingStateStoreHealthCheckOptions();
        configure(options);

        return services.AddIndexThinkingHealthChecks(options);
    }

    // ========================================
    // Metrics (v0.11.0)
    // ========================================

    /// <summary>
    /// Adds IndexThinking metrics with the default meter factory from DI.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers <see cref="IndexThinkingMeter"/> as a singleton,
    /// using the <see cref="IMeterFactory"/> from DI if available.
    /// </para>
    /// <para>
    /// To enable these metrics in OpenTelemetry:
    /// <code>
    /// builder.Services.AddOpenTelemetry()
    ///     .WithMetrics(metrics => metrics.AddMeter("IndexThinking"));
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddIndexThinkingMetrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IndexThinkingMeter>(sp =>
        {
            var meterFactory = sp.GetService<IMeterFactory>();
            return new IndexThinkingMeter(meterFactory);
        });

        return services;
    }

    // ========================================
    // Reasoning Request Modifiers (v0.12.0)
    // ========================================

    /// <summary>
    /// Adds the default reasoning request modifier registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers the default <see cref="ReasoningRequestModifierRegistry"/> which includes
    /// support for DeepSeek, Qwen, vLLM, and GPUStack models.
    /// </para>
    /// <para>
    /// Without a registration, a client with <c>EnableReasoning = true</c> uses
    /// <see cref="ReasoningRequestModifierRegistry.Default"/>. A registered registry is what the DI form of
    /// <c>UseIndexThinking()</c> passes to the client; it does not turn reasoning on by itself.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddIndexThinkingReasoningModifiers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(ReasoningRequestModifierRegistry.Default);

        return services;
    }

    /// <summary>
    /// Adds a custom reasoning request modifier registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for the registry.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Use this to customize which providers and models require explicit reasoning activation.
    /// </para>
    /// <code>
    /// services.AddIndexThinkingReasoningModifiers(registry =>
    /// {
    ///     // Add a custom modifier
    ///     registry.Register(new MyCustomModifier());
    ///
    ///     // Add a custom model prefix mapping
    ///     registry.RegisterModelPrefix("my-model", "my-provider");
    /// });
    /// </code>
    /// </remarks>
    public static IServiceCollection AddIndexThinkingReasoningModifiers(
        this IServiceCollection services,
        Action<ReasoningRequestModifierRegistry> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var registry = new ReasoningRequestModifierRegistry();

        // Start with defaults from the Default registry
        foreach (var provider in ReasoningRequestModifierRegistry.Default.RegisteredProviders)
        {
            var modifier = ReasoningRequestModifierRegistry.Default.GetByProvider(provider);
            if (modifier is not null)
            {
                registry.Register(modifier);
            }
        }
        foreach (var (prefix, provider) in ReasoningRequestModifierRegistry.Default.RegisteredPrefixes)
        {
            registry.RegisterModelPrefix(prefix, provider);
        }

        // Apply custom configuration
        configure(registry);

        services.AddSingleton(registry);

        return services;
    }

    /// <summary>
    /// Adds a custom reasoning request modifier.
    /// </summary>
    /// <typeparam name="TModifier">The modifier type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingReasoningModifier<TModifier>(this IServiceCollection services)
        where TModifier : class, IReasoningRequestModifier
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IReasoningRequestModifier, TModifier>();

        return services;
    }
}
