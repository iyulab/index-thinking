using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Detects if a chat response was truncated due to token limits or other reasons.
/// </summary>
public interface ITruncationDetector
{
    /// <summary>
    /// Determines if the response was truncated.
    /// </summary>
    /// <param name="response">The chat response to check.</param>
    /// <returns>Information about whether and why the response was truncated.</returns>
    TruncationInfo Detect(ChatResponse response);
}
