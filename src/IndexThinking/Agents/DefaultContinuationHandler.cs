using IndexThinking.Abstractions;
using IndexThinking.Continuation;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Default implementation of <see cref="IContinuationHandler"/>.
/// </summary>
/// <remarks>
/// Uses state machine pattern:
/// 1. Detect truncation via ITruncationDetector
/// 2. Apply content recovery if needed
/// 3. Build continuation request with previous response
/// 4. Track progress to prevent infinite loops
/// </remarks>
public sealed class DefaultContinuationHandler : IContinuationHandler
{
    private readonly ITruncationDetector _truncationDetector;

    /// <summary>
    /// Creates a new continuation handler.
    /// </summary>
    /// <param name="truncationDetector">Detector for response truncation.</param>
    public DefaultContinuationHandler(ITruncationDetector truncationDetector)
    {
        _truncationDetector = truncationDetector ?? throw new ArgumentNullException(nameof(truncationDetector));
    }

    /// <inheritdoc />
    public async Task<ContinuationResult> HandleAsync(
        ThinkingContext context,
        ChatResponse initialResponse,
        Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>> sendRequest)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(initialResponse);
        ArgumentNullException.ThrowIfNull(sendRequest);

        var config = context.Continuation;
        var truncationInfo = _truncationDetector.Detect(initialResponse);

        // Not truncated - return immediately
        if (!truncationInfo.IsTruncated)
        {
            return ContinuationResult.NotTruncated(initialResponse);
        }

        var intermediateResponses = new List<ChatResponse> { initialResponse };
        var fragments = new List<string>();
        var continuationCount = 0;
        var currentResponse = initialResponse;

        // Collect initial fragment
        var initialText = GetResponseText(initialResponse);
        if (!string.IsNullOrEmpty(initialText))
        {
            fragments.Add(initialText);
        }

        // Continuation loop
        while (continuationCount < config.MaxContinuations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Check for progress
            var lastFragmentLength = fragments.Count > 0 ? fragments[^1].Length : 0;
            if (fragments.Count > 1 && lastFragmentLength < config.MinProgressPerContinuation)
            {
                break;
            }

            // Apply delay between continuations
            if (continuationCount > 0 && config.DelayBetweenContinuations > TimeSpan.Zero)
            {
                await Task.Delay(config.DelayBetweenContinuations, context.CancellationToken);
            }

            // Build continuation messages
            var continuationMessages = BuildContinuationMessages(context, currentResponse, config);

            // Send continuation request
            var nextResponse = await sendRequest(continuationMessages, context.CancellationToken);
            continuationCount++;
            intermediateResponses.Add(nextResponse);

            // Collect fragment
            var nextText = GetResponseText(nextResponse);
            if (!string.IsNullOrEmpty(nextText))
            {
                fragments.Add(nextText);
            }

            currentResponse = nextResponse;

            // Check if this response is also truncated
            var nextTruncation = _truncationDetector.Detect(nextResponse);
            if (!nextTruncation.IsTruncated)
            {
                break;
            }
        }

        var reachedMax = continuationCount >= config.MaxContinuations;
        if (reachedMax && config.ThrowOnMaxContinuations)
        {
            throw new InvalidOperationException(
                $"Response truncated and max continuations ({config.MaxContinuations}) reached");
        }

        // Combine fragments
        var combinedText = CombineFragments(fragments, config);

        // Apply content recovery
        combinedText = ApplyRecovery(combinedText, config);

        // Build final response
        var finalResponse = BuildFinalResponse(currentResponse, combinedText);

        return new ContinuationResult
        {
            FinalResponse = finalResponse,
            ContinuationCount = continuationCount,
            ReachedMaxContinuations = reachedMax,
            IntermediateResponses = intermediateResponses
        };
    }

    private static IList<ChatMessage> BuildContinuationMessages(
        ThinkingContext context,
        ChatResponse previousResponse,
        ContinuationConfig config)
    {
        var messages = new List<ChatMessage>(context.Messages);

        // Include previous response if configured
        if (config.IncludePreviousResponse)
        {
            var previousText = GetResponseText(previousResponse);
            if (!string.IsNullOrEmpty(previousText))
            {
                messages.Add(new ChatMessage(ChatRole.Assistant, previousText));
            }
        }

        // Add continuation prompt
        messages.Add(new ChatMessage(ChatRole.User, config.ContinuationPrompt));

        return messages;
    }

    private static string GetResponseText(ChatResponse response)
    {
        return response.Text ?? string.Empty;
    }

    private static string CombineFragments(List<string> fragments, ContinuationConfig config)
    {
        if (fragments.Count == 0)
        {
            return string.Empty;
        }

        if (fragments.Count == 1)
        {
            return fragments[0];
        }

        return ContentRecoveryUtils.CombineFragments(fragments);
    }

    private static string ApplyRecovery(string text, ContinuationConfig config)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Try JSON recovery
        if (config.EnableJsonRecovery)
        {
            var jsonResult = ContentRecoveryUtils.TryRecoverJson(text);
            if (jsonResult.Status == ContentRecoveryStatus.Recovered ||
                jsonResult.Status == ContentRecoveryStatus.PartiallyRecovered)
            {
                return jsonResult.Content;
            }
        }

        // Try code block recovery
        if (config.EnableCodeBlockRecovery)
        {
            var codeResult = ContentRecoveryUtils.TryRecoverCodeBlocks(text);
            if (codeResult.Status == ContentRecoveryStatus.Recovered ||
                codeResult.Status == ContentRecoveryStatus.PartiallyRecovered)
            {
                return codeResult.Content;
            }
        }

        return text;
    }

    private static ChatResponse BuildFinalResponse(ChatResponse lastResponse, string combinedText)
    {
        var message = new ChatMessage(ChatRole.Assistant, combinedText);

        // Preserve metadata from last response
        return new ChatResponse([message])
        {
            FinishReason = lastResponse.FinishReason,
            ModelId = lastResponse.ModelId,
            Usage = lastResponse.Usage,
            AdditionalProperties = lastResponse.AdditionalProperties
        };
    }
}
