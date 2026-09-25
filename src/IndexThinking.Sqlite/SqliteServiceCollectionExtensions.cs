using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IndexThinking.Abstractions;
using IndexThinking.Stores;

namespace IndexThinking.Extensions;

/// <summary>
/// Registers the SQLite-backed <see cref="IThinkingStateStore"/> (package <c>IndexThinking.Sqlite</c>).
/// </summary>
public static class SqliteServiceCollectionExtensions
{
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
}
