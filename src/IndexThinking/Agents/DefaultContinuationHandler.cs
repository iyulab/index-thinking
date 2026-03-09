using System.Diagnostics;
using IndexThinking.Abstractions;
using IndexThinking.Continuation;
using IndexThinking.Parsers;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Default implementation of <see cref="IContinuationHandler"/>.
/// </summary>
/// <remarks>
/// Uses state machine pattern:
/// 1. Detect truncation via ITruncationDetector
/// 2. Validate context token budget before continuation
/// 3. Apply content recovery if needed
/// 4. Build continuation request with previous response
/// 5. Track progress to prevent infinite loops
/// </remarks>
public sealed class DefaultContinuationHandler : IContinuationHandler
{
    private readonly ITruncationDetector _truncationDetector;
    private readonly ITokenCounter _tokenCounter;

    /// <summary>
    /// Creates a new continuation handler.
    /// </summary>
    /// <param name="truncationDetector">Detector for response truncation.</param>
    /// <param name="tokenCounter">Token counter for context budget validation.</param>
    public DefaultContinuationHandler(ITruncationDetector truncationDetector, ITokenCounter tokenCounter)
    {
        _truncationDetector = truncationDetector ?? throw new ArgumentNullException(nameof(truncationDetector));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
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

        // Capture prompt token baseline from initial response usage (more accurate than estimation)
        var promptTokenBaseline = (int?)(initialResponse.Usage?.InputTokenCount);

        var intermediateResponses = new List<ChatResponse> { initialResponse };
        var fragments = new List<string>();
        var continuationCount = 0;
        var currentResponse = initialResponse;
        var elapsed = Stopwatch.StartNew();

        // Collect initial fragment (strip think tags per-fragment so combined text is clean)
        var initialText = GetResponseText(initialResponse);
        if (!string.IsNullOrEmpty(initialText))
        {
            fragments.Add(OpenSourceReasoningParser.StripThinkTags(initialText));
        }

        // Continuation loop
        while (continuationCount < config.MaxContinuations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Enforce MaxTotalDuration
            if (elapsed.Elapsed >= config.MaxTotalDuration)
            {
                break;
            }

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

            // Validate context token budget — if over limit, compact messages
            if (config.MaxContextTokens is > 0)
            {
                var estimatedTokens = EstimateContinuationTokens(
                    continuationMessages, promptTokenBaseline, currentResponse);

                if (estimatedTokens > config.MaxContextTokens.Value)
                {
                    // Compact: drop original user content, keep system + response tail
                    continuationMessages = BuildCompactContinuationMessages(
                        context, currentResponse, config, config.MaxContextTokens.Value);

                    // Re-estimate with compacted messages
                    var compactTokens = 0;
                    foreach (var msg in continuationMessages)
                    {
                        compactTokens += _tokenCounter.Count(msg);
                    }

                    if (compactTokens > config.MaxContextTokens.Value)
                    {
                        break; // Even compacted messages don't fit
                    }

                    // Reset baseline for compacted requests
                    promptTokenBaseline = null;
                }
            }

            // Send continuation request
            var nextResponse = await sendRequest(continuationMessages, context.CancellationToken);
            continuationCount++;
            intermediateResponses.Add(nextResponse);

            // Collect fragment (strip think tags per-fragment so combined text is clean)
            var nextText = GetResponseText(nextResponse);
            if (!string.IsNullOrEmpty(nextText))
            {
                fragments.Add(OpenSourceReasoningParser.StripThinkTags(nextText));
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

        // Strip trailing untagged reasoning (continuation artifacts where the model
        // responds with inline reasoning instead of actual content)
        combinedText = OpenSourceReasoningParser.StripUntaggedReasoning(combinedText);

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

    /// <summary>
    /// Estimates the total token count for a continuation request.
    /// Prefers the initial response's prompt_tokens as baseline when available,
    /// since it reflects the actual tokenization the model performed.
    /// </summary>
    private int EstimateContinuationTokens(
        IList<ChatMessage> continuationMessages,
        int? promptTokenBaseline,
        ChatResponse currentResponse)
    {
        if (promptTokenBaseline.HasValue)
        {
            // Use actual prompt_tokens from initial request as baseline for original messages,
            // then estimate only the added messages (assistant response + continuation prompt)
            var additionalTokens = 0;
            var previousText = GetResponseText(currentResponse);
            if (!string.IsNullOrEmpty(previousText))
            {
                previousText = OpenSourceReasoningParser.StripThinkTags(previousText);
                additionalTokens += _tokenCounter.Count(previousText);
            }

            // Continuation prompt tokens
            additionalTokens += _tokenCounter.Count(
                continuationMessages[^1].Text ?? string.Empty);

            return promptTokenBaseline.Value + additionalTokens;
        }

        // Fallback: estimate all messages
        var total = 0;
        foreach (var message in continuationMessages)
        {
            total += _tokenCounter.Count(message);
        }
        return total;
    }

    private static List<ChatMessage> BuildContinuationMessages(
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
                // Strip think tags from assistant context so the model
                // doesn't treat its own reasoning as conversation content
                previousText = OpenSourceReasoningParser.StripThinkTags(previousText);
                messages.Add(new ChatMessage(ChatRole.Assistant, previousText));
            }
        }

        // Add continuation prompt
        messages.Add(new ChatMessage(ChatRole.User, config.ContinuationPrompt));

        return messages;
    }

    /// <summary>
    /// Builds compacted continuation messages when full messages exceed context limit.
    /// Drops original user content (already processed), keeps system prompt +
    /// tail of previous response + continuation prompt.
    /// </summary>
    private List<ChatMessage> BuildCompactContinuationMessages(
        ThinkingContext context,
        ChatResponse previousResponse,
        ContinuationConfig config,
        int maxContextTokens)
    {
        var messages = new List<ChatMessage>();

        // Keep system message only (format instructions)
        var systemMessage = context.Messages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMessage is not null)
        {
            messages.Add(systemMessage);
        }

        // Include previous response (the model needs to know where it left off)
        if (config.IncludePreviousResponse)
        {
            var previousText = GetResponseText(previousResponse);
            if (!string.IsNullOrEmpty(previousText))
            {
                previousText = OpenSourceReasoningParser.StripThinkTags(previousText);

                // Calculate budget for the response tail
                var systemTokens = systemMessage is not null ? _tokenCounter.Count(systemMessage) : 0;
                var promptTokens = _tokenCounter.Count(config.ContinuationPrompt);
                // Reserve half the remaining budget for the model's output
                var responseBudget = (maxContextTokens - systemTokens - promptTokens) / 2;

                if (responseBudget > 0)
                {
                    var responseTokens = _tokenCounter.Count(previousText);
                    if (responseTokens > responseBudget)
                    {
                        // Truncate to tail: keep the last portion
                        previousText = TruncateToTail(previousText, responseBudget);
                    }
                }

                messages.Add(new ChatMessage(ChatRole.Assistant, previousText));
            }
        }

        // Add continuation prompt
        messages.Add(new ChatMessage(ChatRole.User, config.ContinuationPrompt));

        return messages;
    }

    /// <summary>
    /// Truncates text to approximately the last N tokens worth of content.
    /// Tries to break at a newline boundary for cleaner continuation.
    /// </summary>
    private string TruncateToTail(string text, int targetTokens)
    {
        // Approximate: use ratio of target/total tokens to determine character position
        var totalTokens = _tokenCounter.Count(text);
        if (totalTokens <= targetTokens)
        {
            return text;
        }

        var ratio = (double)targetTokens / totalTokens;
        var startChar = (int)(text.Length * (1 - ratio));

        // Try to find a newline boundary near the cut point
        var newlinePos = text.IndexOf('\n', startChar);
        if (newlinePos >= 0 && newlinePos < startChar + 200)
        {
            startChar = newlinePos + 1;
        }

        return text[startChar..];
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
