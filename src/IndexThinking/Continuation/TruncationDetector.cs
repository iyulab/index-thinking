using System.Text.RegularExpressions;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Continuation;

/// <summary>
/// Detects truncation in LLM responses using multiple strategies.
/// </summary>
/// <remarks>
/// <para>Detection strategies:</para>
/// <list type="number">
/// <item>FinishReason analysis (primary) - Most reliable, provider-specific mapping</item>
/// <item>Structural analysis (secondary) - Unbalanced braces, incomplete code blocks</item>
/// <item>Heuristic analysis (tertiary) - Mid-sentence endings</item>
/// </list>
///
/// <para>Provider-specific finish reasons handled:</para>
/// <list type="bullet">
/// <item><b>OpenAI</b>: stop, length, content_filter, tool_calls, function_call</item>
/// <item><b>Anthropic</b>: end_turn, max_tokens, stop_sequence, tool_use, refusal, model_context_window_exceeded</item>
/// <item><b>Google Gemini</b>: STOP, MAX_TOKENS, SAFETY, RECITATION</item>
/// <item><b>GPUStack/vLLM</b>: OpenAI-compatible (stop, length)</item>
/// </list>
///
/// <para>Microsoft.Extensions.AI SDK mappings:</para>
/// <list type="bullet">
/// <item><see cref="ChatFinishReason.Stop"/> - Normal completion</item>
/// <item><see cref="ChatFinishReason.Length"/> - Token limit (length, max_tokens, MAX_TOKENS)</item>
/// <item><see cref="ChatFinishReason.ContentFilter"/> - Content filtered</item>
/// <item><see cref="ChatFinishReason.ToolCalls"/> - Tool/function call</item>
/// </list>
/// </remarks>
public sealed class TruncationDetector : ITruncationDetector
{
    private static readonly Regex CodeBlockPattern = new(@"^```[\w+#]*\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex SentenceEndPattern = new(@"[.!?\u3002\u3001\uFF01\uFF1F]\s*$", RegexOptions.Compiled);

    private readonly TruncationDetectorOptions _options;

    /// <summary>
    /// Creates a new truncation detector with default options.
    /// </summary>
    public TruncationDetector() : this(new TruncationDetectorOptions())
    {
    }

    /// <summary>
    /// Creates a new truncation detector with the specified options.
    /// </summary>
    /// <param name="options">Detection options.</param>
    public TruncationDetector(TruncationDetectorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public TruncationInfo Detect(ChatResponse response)
    {
        if (response is null)
        {
            return TruncationInfo.NotTruncated;
        }

        // Strategy 1: Check FinishReason (most reliable)
        var finishReasonResult = CheckFinishReason(response);
        if (finishReasonResult.IsTruncated)
        {
            return finishReasonResult;
        }

        // Get response text for structural analysis
        var text = ExtractText(response);
        if (string.IsNullOrEmpty(text))
        {
            return TruncationInfo.NotTruncated;
        }

        // Strategy 2: Check structural completeness
        if (_options.EnableStructuralAnalysis)
        {
            var structuralResult = CheckStructuralCompleteness(text);
            if (structuralResult.IsTruncated)
            {
                return structuralResult;
            }
        }

        // Strategy 3: Check for mid-sentence ending (heuristic)
        if (_options.EnableHeuristicAnalysis)
        {
            var heuristicResult = CheckMidSentence(text);
            if (heuristicResult.IsTruncated)
            {
                return heuristicResult;
            }
        }

        return TruncationInfo.NotTruncated;
    }

    /// <summary>
    /// Detects truncation in raw text without response context.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>Truncation information.</returns>
    public TruncationInfo DetectInText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return TruncationInfo.NotTruncated;
        }

        if (_options.EnableStructuralAnalysis)
        {
            var structuralResult = CheckStructuralCompleteness(text);
            if (structuralResult.IsTruncated)
            {
                return structuralResult;
            }
        }

        if (_options.EnableHeuristicAnalysis)
        {
            var heuristicResult = CheckMidSentence(text);
            if (heuristicResult.IsTruncated)
            {
                return heuristicResult;
            }
        }

        return TruncationInfo.NotTruncated;
    }

    private static TruncationInfo CheckFinishReason(ChatResponse response)
    {
        var finishReason = response.FinishReason;

        if (finishReason is null)
        {
            return TruncationInfo.NotTruncated;
        }

        // ChatFinishReason.Length indicates token limit reached (standard abstraction)
        // This covers: OpenAI "length", Anthropic "max_tokens" (via SDK mapping), Google "MAX_TOKENS"
        if (finishReason == ChatFinishReason.Length)
        {
            return TruncationInfo.Truncated(
                TruncationReason.TokenLimit,
                "Response was truncated due to token limit (finish_reason: length)");
        }

        // ChatFinishReason.ContentFilter indicates content was filtered
        // This covers: OpenAI "content_filter"
        if (finishReason == ChatFinishReason.ContentFilter)
        {
            return TruncationInfo.Truncated(
                TruncationReason.ContentFiltered,
                "Response was blocked by content filter");
        }

        // Check for provider-specific stop reasons via Value property
        // This handles cases where the SDK doesn't map to standard ChatFinishReason
        var reasonValue = finishReason.Value.Value;
        if (string.IsNullOrEmpty(reasonValue))
        {
            return TruncationInfo.NotTruncated;
        }

        // Token limit variations
        // Anthropic: "max_tokens", Google: "MAX_TOKENS" (if not mapped by SDK)
        if (string.Equals(reasonValue, "max_tokens", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonValue, "MAX_TOKENS", StringComparison.OrdinalIgnoreCase))
        {
            return TruncationInfo.Truncated(
                TruncationReason.TokenLimit,
                $"Response was truncated due to token limit (stop_reason: {reasonValue})");
        }

        // Context window exceeded
        // Anthropic: "model_context_window_exceeded"
        if (string.Equals(reasonValue, "model_context_window_exceeded", StringComparison.OrdinalIgnoreCase))
        {
            return TruncationInfo.Truncated(
                TruncationReason.ContextWindowExceeded,
                "Response was truncated due to context window limit exceeded");
        }

        // Safety/Content filter variations
        // OpenAI: "content_filter", Google: "SAFETY"
        if (string.Equals(reasonValue, "content_filter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonValue, "SAFETY", StringComparison.OrdinalIgnoreCase))
        {
            return TruncationInfo.Truncated(
                TruncationReason.ContentFiltered,
                $"Response was blocked by safety/content filter (stop_reason: {reasonValue})");
        }

        // Recitation (potential copyright issue)
        // Google: "RECITATION"
        if (string.Equals(reasonValue, "RECITATION", StringComparison.OrdinalIgnoreCase))
        {
            return TruncationInfo.Truncated(
                TruncationReason.Recitation,
                "Response was stopped due to potential recitation/copyright concerns");
        }

        // Refusal (safety refusal)
        // Anthropic: "refusal"
        if (string.Equals(reasonValue, "refusal", StringComparison.OrdinalIgnoreCase))
        {
            return TruncationInfo.Truncated(
                TruncationReason.Refusal,
                "Model refused to generate response due to safety concerns");
        }

        return TruncationInfo.NotTruncated;
    }

    private static TruncationInfo CheckStructuralCompleteness(string text)
    {
        // Check for unbalanced braces/brackets
        var braceResult = CheckUnbalancedBraces(text);
        if (braceResult.IsTruncated)
        {
            return braceResult;
        }

        // Check for incomplete code blocks
        var codeBlockResult = CheckIncompleteCodeBlock(text);
        if (codeBlockResult.IsTruncated)
        {
            return codeBlockResult;
        }

        return TruncationInfo.NotTruncated;
    }

    private static TruncationInfo CheckUnbalancedBraces(string text)
    {
        var braceCount = 0;
        var bracketCount = 0;
        var parenCount = 0;
        var inString = false;
        var stringChar = '\0';
        var escaped = false;

        foreach (var c in text)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (inString)
            {
                if (c == stringChar)
                {
                    inString = false;
                }
                continue;
            }

            switch (c)
            {
                case '"':
                case '\'':
                    inString = true;
                    stringChar = c;
                    break;
                case '{':
                    braceCount++;
                    break;
                case '}':
                    braceCount--;
                    break;
                case '[':
                    bracketCount++;
                    break;
                case ']':
                    bracketCount--;
                    break;
                case '(':
                    parenCount++;
                    break;
                case ')':
                    parenCount--;
                    break;
            }
        }

        if (braceCount > 0 || bracketCount > 0 || parenCount > 0)
        {
            var details = new List<string>();
            if (braceCount > 0) details.Add($"{braceCount} unclosed '{{' brace(s)");
            if (bracketCount > 0) details.Add($"{bracketCount} unclosed '[' bracket(s)");
            if (parenCount > 0) details.Add($"{parenCount} unclosed '(' parenthesis(es)");

            return TruncationInfo.Truncated(
                TruncationReason.UnbalancedStructure,
                string.Join(", ", details));
        }

        return TruncationInfo.NotTruncated;
    }

    private static TruncationInfo CheckIncompleteCodeBlock(string text)
    {
        // Use state-based tracking: each ``` line toggles code block state
        var matches = CodeBlockPattern.Matches(text);
        var insideCodeBlock = false;

        foreach (Match _ in matches)
        {
            insideCodeBlock = !insideCodeBlock;
        }

        if (insideCodeBlock)
        {
            return TruncationInfo.Truncated(
                TruncationReason.IncompleteCodeBlock,
                "1 unclosed code block(s) detected");
        }

        return TruncationInfo.NotTruncated;
    }

    private TruncationInfo CheckMidSentence(string text)
    {
        if (text.Length < _options.MinTextLengthForHeuristics)
        {
            return TruncationInfo.NotTruncated;
        }

        var trimmed = text.TrimEnd();
        if (string.IsNullOrEmpty(trimmed))
        {
            return TruncationInfo.NotTruncated;
        }

        // Skip heuristic if text ends with code block
        if (trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return TruncationInfo.NotTruncated;
        }

        // Check if text ends with proper sentence termination
        if (!SentenceEndPattern.IsMatch(trimmed))
        {
            // Additional check: don't flag if ends with list marker or header
            var lastChar = trimmed[^1];
            if (lastChar == ':' || lastChar == '-' || lastChar == '*' || lastChar == '#')
            {
                return TruncationInfo.NotTruncated;
            }

            return TruncationInfo.Truncated(
                TruncationReason.MidSentence,
                "Response appears to end mid-sentence");
        }

        return TruncationInfo.NotTruncated;
    }

    private static string ExtractText(ChatResponse response)
    {
        // ChatResponse.Text combines all message text
        if (!string.IsNullOrEmpty(response.Text))
        {
            return response.Text;
        }

        // Fallback: extract from Messages collection
        return response.Messages?
            .SelectMany(m => m.Contents ?? [])
            .OfType<TextContent>()
            .FirstOrDefault()?.Text ?? string.Empty;
    }
}

/// <summary>
/// Configuration options for truncation detection.
/// </summary>
public sealed record TruncationDetectorOptions
{
    /// <summary>
    /// Whether to enable structural analysis (unbalanced braces, code blocks).
    /// Default: true
    /// </summary>
    public bool EnableStructuralAnalysis { get; init; } = true;

    /// <summary>
    /// Whether to enable heuristic analysis (mid-sentence detection).
    /// Default: true
    /// </summary>
    public bool EnableHeuristicAnalysis { get; init; } = true;

    /// <summary>
    /// Minimum text length before applying heuristic analysis.
    /// Shorter texts are assumed to be intentionally brief.
    /// Default: 100 characters
    /// </summary>
    public int MinTextLengthForHeuristics { get; init; } = 100;
}
