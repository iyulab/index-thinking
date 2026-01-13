using System.Text.RegularExpressions;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Continuation;

/// <summary>
/// Detects truncation in LLM responses using multiple strategies.
/// </summary>
/// <remarks>
/// Detection strategies:
/// 1. FinishReason analysis (primary) - ChatFinishReason.Length indicates truncation
/// 2. Structural analysis (secondary) - Unbalanced braces, incomplete code blocks
/// 3. Heuristic analysis (tertiary) - Mid-sentence endings
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

        // ChatFinishReason.Length indicates token limit reached
        if (finishReason == ChatFinishReason.Length)
        {
            return TruncationInfo.Truncated(
                TruncationReason.TokenLimit,
                "Response was truncated due to token limit (finish_reason: length)");
        }

        // Check for provider-specific stop reasons via Value property
        var reasonValue = finishReason.Value.Value;
        if (string.Equals(reasonValue, "max_tokens", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reasonValue, "model_context_window_exceeded", StringComparison.OrdinalIgnoreCase))
        {
            return TruncationInfo.Truncated(
                TruncationReason.TokenLimit,
                $"Response was truncated (stop_reason: {reasonValue})");
        }

        return TruncationInfo.NotTruncated;
    }

    private TruncationInfo CheckStructuralCompleteness(string text)
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
        if (trimmed.EndsWith("```"))
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
