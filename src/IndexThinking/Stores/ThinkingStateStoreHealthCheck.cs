using Microsoft.Extensions.Diagnostics.HealthChecks;
using IndexThinking.Abstractions;

namespace IndexThinking.Stores;

/// <summary>
/// Health check for <see cref="IThinkingStateStore"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// Performs a simple read operation to verify the store is accessible.
/// Uses a well-known test session ID to avoid polluting the store.
/// </para>
/// <para>
/// Register using:
/// <code>
/// services.AddHealthChecks()
///     .AddCheck&lt;ThinkingStateStoreHealthCheck&gt;("thinking-state-store");
/// </code>
/// </para>
/// </remarks>
public sealed class ThinkingStateStoreHealthCheck : IHealthCheck
{
    private readonly IThinkingStateStore _store;
    private readonly ThinkingStateStoreHealthCheckOptions _options;

    /// <summary>
    /// Creates a new health check for the thinking state store.
    /// </summary>
    /// <param name="store">The state store to check.</param>
    /// <param name="options">Configuration options.</param>
    public ThinkingStateStoreHealthCheck(
        IThinkingStateStore store,
        ThinkingStateStoreHealthCheckOptions? options = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new ThinkingStateStoreHealthCheckOptions();
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.Timeout);

            // Perform a simple existence check with a well-known test key
            var testSessionId = _options.TestSessionId;
            var exists = await _store.ExistsAsync(testSessionId, cts.Token);

            var data = new Dictionary<string, object>
            {
                ["store_type"] = _store.GetType().Name,
                ["test_session_id"] = testSessionId,
                ["session_exists"] = exists
            };

            return HealthCheckResult.Healthy(
                $"ThinkingStateStore ({_store.GetType().Name}) is responsive",
                data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded(
                $"ThinkingStateStore health check timed out after {_options.Timeout.TotalSeconds}s",
                data: new Dictionary<string, object>
                {
                    ["store_type"] = _store.GetType().Name,
                    ["timeout_seconds"] = _options.Timeout.TotalSeconds
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"ThinkingStateStore ({_store.GetType().Name}) is not accessible",
                ex,
                new Dictionary<string, object>
                {
                    ["store_type"] = _store.GetType().Name,
                    ["error_type"] = ex.GetType().Name
                });
        }
    }
}

/// <summary>
/// Configuration options for <see cref="ThinkingStateStoreHealthCheck"/>.
/// </summary>
public sealed class ThinkingStateStoreHealthCheckOptions
{
    /// <summary>
    /// Timeout for the health check operation. Default: 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Test session ID used for existence checks. Default: "__health_check__".
    /// </summary>
    /// <remarks>
    /// This session ID is used only for read operations and won't create any data.
    /// </remarks>
    public string TestSessionId { get; set; } = "__health_check__";
}
