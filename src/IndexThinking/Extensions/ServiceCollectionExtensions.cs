using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Continuation;
using IndexThinking.Core;
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
}
