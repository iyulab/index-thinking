using IndexThinking.Agents;
using IndexThinking.Continuation;
using IndexThinking.Core;

namespace IndexThinking.Client;

/// <summary>
/// Configuration options for <see cref="ThinkingChatClient"/>.
/// </summary>
public class ThinkingChatClientOptions
{
    /// <summary>
    /// Default budget configuration for all requests.
    /// Can be overridden per-request via ChatOptions.
    /// </summary>
    public BudgetConfig DefaultBudget { get; set; } = new();

    /// <summary>
    /// Default continuation configuration for handling truncated responses.
    /// </summary>
    public ContinuationConfig DefaultContinuation { get; set; } = ContinuationConfig.Default;

    /// <summary>
    /// Whether to automatically estimate task complexity.
    /// </summary>
    public bool AutoEstimateComplexity { get; set; } = true;

    /// <summary>
    /// Whether to include thinking content in the response metadata.
    /// </summary>
    public bool IncludeThinkingInMetadata { get; set; } = true;

    /// <summary>
    /// Whether to include turn metrics in the response metadata.
    /// </summary>
    public bool IncludeMetricsInMetadata { get; set; } = true;

    /// <summary>
    /// Key used to store session ID in ChatOptions.AdditionalProperties.
    /// </summary>
    public string SessionIdKey { get; set; } = "IndexThinking.SessionId";

    /// <summary>
    /// Factory for generating session IDs when not provided.
    /// Defaults to generating a new GUID.
    /// </summary>
    public Func<string> SessionIdFactory { get; set; } = () => Guid.NewGuid().ToString("N");
}
