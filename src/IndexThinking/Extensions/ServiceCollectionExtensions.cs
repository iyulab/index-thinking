using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Context;
using IndexThinking.Continuation;
using IndexThinking.Core;
using IndexThinking.Memory;
using IndexThinking.Stores;

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
    /// Adds a SQLite-based thinking state store with the specified connection string.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingSqliteStorage(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddIndexThinkingSqliteStorage(new SqliteStateStoreOptions
        {
            ConnectionString = connectionString
        });
    }

    /// <summary>
    /// Adds a SQLite-based thinking state store with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Configuration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingSqliteStorage(
        this IServiceCollection services,
        SqliteStateStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IThinkingStateStore, SqliteThinkingStateStore>();

        return services;
    }

    /// <summary>
    /// Adds a SQLite-based thinking state store with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Options configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingSqliteStorage(
        this IServiceCollection services,
        Action<SqliteStateStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // SqliteStateStoreOptions requires ConnectionString, so we need a different approach
        var options = new SqliteStateStoreOptions { ConnectionString = string.Empty };
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("ConnectionString must be configured.");
        }

        return services.AddIndexThinkingSqliteStorage(options);
    }

    /// <summary>
    /// Adds IndexThinking turn management services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for agent options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingAgents(
        this IServiceCollection services,
        Action<AgentOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register core agent services
        services.TryAddSingleton<IComplexityEstimator, HeuristicComplexityEstimator>();
        services.TryAddSingleton<IBudgetTracker, DefaultBudgetTracker>();
        services.TryAddSingleton<IContinuationHandler, DefaultContinuationHandler>();
        services.TryAddSingleton<IThinkingTurnManager, DefaultThinkingTurnManager>();

        // Configure options
        if (configure is not null)
        {
            var options = new AgentOptions();
            configure(options);
            services.TryAddSingleton(options);
        }
        else
        {
            services.TryAddSingleton(new AgentOptions());
        }

        return services;
    }

    /// <summary>
    /// Adds the null memory provider (no-op, zero-config mode).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This is the default when no memory provider is explicitly configured.
    /// Memory-dependent features are disabled.
    /// </remarks>
    public static IServiceCollection AddIndexThinkingNullMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMemoryProvider>(NullMemoryProvider.Instance);

        return services;
    }

    /// <summary>
    /// Adds a function-based memory provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="recallDelegate">The delegate that performs memory recall.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Use this to integrate with any memory backend without direct dependencies.
    /// </para>
    /// <para>
    /// Example with Memory-Indexer:
    /// </para>
    /// <code>
    /// services.AddIndexThinkingMemory(async (userId, sessionId, query, limit, ct) =>
    /// {
    ///     var context = await memoryService.RecallAsync(userId, sessionId, query, limit, ct);
    ///     return new MemoryRecallResult
    ///     {
    ///         UserMemories = context.UserMemories.Select(m => (m.Content, m.Relevance)).ToList(),
    ///         SessionMemories = context.SessionMemories.Select(m => (m.Content, m.Relevance)).ToList(),
    ///         TopicMemories = context.TopicMemories.Select(m => (m.Content, m.Relevance)).ToList()
    ///     };
    /// });
    /// </code>
    /// </remarks>
    public static IServiceCollection AddIndexThinkingMemory(
        this IServiceCollection services,
        MemoryRecallDelegate recallDelegate)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(recallDelegate);

        services.AddSingleton<IMemoryProvider>(new FuncMemoryProvider(recallDelegate));

        return services;
    }

    /// <summary>
    /// Adds a custom memory provider.
    /// </summary>
    /// <typeparam name="TProvider">The memory provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingMemory<TProvider>(this IServiceCollection services)
        where TProvider : class, IMemoryProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMemoryProvider, TProvider>();

        return services;
    }

    /// <summary>
    /// Adds a custom memory provider instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="provider">The memory provider instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingMemory(
        this IServiceCollection services,
        IMemoryProvider provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);

        services.AddSingleton(provider);

        return services;
    }

    /// <summary>
    /// Adds context tracking services with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="IContextTracker"/> - In-memory context tracker</item>
    ///   <item><see cref="IContextInjector"/> - Default context injector</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddIndexThinkingContext(this IServiceCollection services)
    {
        return services.AddIndexThinkingContext(
            ContextTrackerOptions.Default,
            ContextInjectorOptions.Default);
    }

    /// <summary>
    /// Adds context tracking services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureTracker">Tracker options configuration.</param>
    /// <param name="configureInjector">Injector options configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingContext(
        this IServiceCollection services,
        Action<ContextTrackerOptions>? configureTracker = null,
        Action<ContextInjectorOptions>? configureInjector = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var trackerOptions = new ContextTrackerOptions();
        configureTracker?.Invoke(trackerOptions);

        var injectorOptions = new ContextInjectorOptions();
        configureInjector?.Invoke(injectorOptions);

        return services.AddIndexThinkingContext(trackerOptions, injectorOptions);
    }

    /// <summary>
    /// Adds context tracking services with specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="trackerOptions">Context tracker options.</param>
    /// <param name="injectorOptions">Context injector options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIndexThinkingContext(
        this IServiceCollection services,
        ContextTrackerOptions trackerOptions,
        ContextInjectorOptions injectorOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(trackerOptions);
        ArgumentNullException.ThrowIfNull(injectorOptions);

        services.TryAddSingleton(trackerOptions);
        services.TryAddSingleton<IContextTracker, InMemoryContextTracker>();

        services.TryAddSingleton(injectorOptions);
        services.TryAddSingleton<IContextInjector, DefaultContextInjector>();

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
}
