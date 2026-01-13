using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IndexThinking.Continuation;

/// <summary>
/// Utilities for recovering incomplete content from truncated responses.
/// </summary>
public static class ContentRecoveryUtils
{
    private static readonly Regex CodeBlockStartPattern = new(@"```[\w+#]*\s*\n", RegexOptions.Compiled);
    private static readonly Regex IncompleteJsonPattern = new(@"[\[{](?:[^{}\[\]]|(?<open>[\[{])|(?<-open>[}\]]))*$", RegexOptions.Compiled);

    /// <summary>
    /// Attempts to recover truncated JSON by adding missing closing brackets/braces.
    /// </summary>
    /// <param name="json">The potentially incomplete JSON string.</param>
    /// <returns>Recovery result with the repaired JSON if successful.</returns>
    public static ContentRecoveryResult TryRecoverJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ContentRecoveryResult.Failed("Input is null or empty");
        }

        var trimmed = json.Trim();

        // Check if already valid JSON
        if (IsValidJson(trimmed))
        {
            return ContentRecoveryResult.NoRecoveryNeeded(trimmed);
        }

        // Try to repair by adding missing closures
        var repaired = RepairJsonClosures(trimmed);
        if (IsValidJson(repaired))
        {
            return ContentRecoveryResult.Recovered(repaired, "Added missing JSON closures");
        }

        // Try truncating to last complete element
        var truncatedToValid = TruncateToValidJson(trimmed);
        if (truncatedToValid is not null && IsValidJson(truncatedToValid))
        {
            return ContentRecoveryResult.PartiallyRecovered(truncatedToValid, "Truncated to last complete element");
        }

        return ContentRecoveryResult.Failed("Unable to recover JSON");
    }

    /// <summary>
    /// Closes any unclosed code blocks in the text.
    /// </summary>
    /// <param name="text">The text containing potential unclosed code blocks.</param>
    /// <returns>Recovery result with closed code blocks.</returns>
    public static ContentRecoveryResult TryRecoverCodeBlocks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ContentRecoveryResult.NoRecoveryNeeded(text ?? string.Empty);
        }

        // Use state-based tracking for accurate code block detection
        // A line starting with ``` toggles the code block state
        var codeBlockPattern = new Regex(@"^```[\w+#]*\s*$", RegexOptions.Multiline);
        var matches = codeBlockPattern.Matches(text);

        // Count open blocks by toggling state for each match
        var insideCodeBlock = false;
        foreach (Match _ in matches)
        {
            insideCodeBlock = !insideCodeBlock;
        }

        // If we end inside a code block, it's unclosed
        if (!insideCodeBlock)
        {
            return ContentRecoveryResult.NoRecoveryNeeded(text);
        }

        // Add missing closure
        var builder = new StringBuilder(text);

        // Ensure text ends with newline before adding closure
        if (!text.EndsWith('\n'))
        {
            builder.AppendLine();
        }

        builder.AppendLine("```");

        return ContentRecoveryResult.Recovered(
            builder.ToString(),
            "Closed 1 code block(s)");
    }

    /// <summary>
    /// Attempts to find a clean truncation point in the text.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>The index of the last clean break point, or -1 if none found.</returns>
    public static int FindCleanTruncationPoint(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return -1;
        }

        // Look for sentence endings (., !, ?, etc.)
        var sentenceEnders = new[] { '.', '!', '?', '\u3002', '\u3001' };
        var lastSentenceEnd = -1;

        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (sentenceEnders.Contains(text[i]))
            {
                lastSentenceEnd = i + 1;
                break;
            }
        }

        if (lastSentenceEnd > 0)
        {
            return lastSentenceEnd;
        }

        // Look for paragraph breaks
        var lastParagraph = text.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (lastParagraph > 0)
        {
            return lastParagraph + 2;
        }

        // Look for line breaks
        var lastLine = text.LastIndexOf('\n');
        if (lastLine > 0)
        {
            return lastLine + 1;
        }

        return -1;
    }

    /// <summary>
    /// Combines multiple response fragments into a single coherent response.
    /// </summary>
    /// <param name="fragments">The response fragments to combine.</param>
    /// <returns>The combined text.</returns>
    public static string CombineFragments(IEnumerable<string> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        var builder = new StringBuilder();
        var isFirst = true;

        foreach (var fragment in fragments)
        {
            if (string.IsNullOrEmpty(fragment))
            {
                continue;
            }

            if (!isFirst)
            {
                // Check if we need a separator
                if (!builder.ToString().EndsWith('\n') && !fragment.StartsWith('\n'))
                {
                    // Don't add separator if previous ends with punctuation
                    var lastChar = builder[^1];
                    if (!char.IsPunctuation(lastChar) && !char.IsWhiteSpace(lastChar))
                    {
                        builder.Append(' ');
                    }
                }
            }

            builder.Append(fragment);
            isFirst = false;
        }

        return builder.ToString();
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string RepairJsonClosures(string json)
    {
        var builder = new StringBuilder(json);
        var bracketStack = new Stack<char>();
        var inString = false;
        var escaped = false;

        foreach (var c in json)
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

            if (c == '"' && !escaped)
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            switch (c)
            {
                case '{':
                    bracketStack.Push('}');
                    break;
                case '[':
                    bracketStack.Push(']');
                    break;
                case '}':
                case ']':
                    if (bracketStack.Count > 0 && bracketStack.Peek() == c)
                    {
                        bracketStack.Pop();
                    }
                    break;
            }
        }

        // Close unclosed string
        if (inString)
        {
            builder.Append('"');
        }

        // Close unclosed brackets
        while (bracketStack.Count > 0)
        {
            builder.Append(bracketStack.Pop());
        }

        return builder.ToString();
    }

    private static string? TruncateToValidJson(string json)
    {
        // Try to find the last complete JSON element
        for (var i = json.Length - 1; i > 0; i--)
        {
            var c = json[i];
            if (c == '}' || c == ']' || c == '"')
            {
                var candidate = json[..(i + 1)];

                // Need to close any outer structures
                var repaired = RepairJsonClosures(candidate);
                if (IsValidJson(repaired))
                {
                    return repaired;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Result of a content recovery operation.
/// </summary>
public sealed record ContentRecoveryResult
{
    /// <summary>
    /// The recovery status.
    /// </summary>
    public required ContentRecoveryStatus Status { get; init; }

    /// <summary>
    /// The recovered or original content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Description of what was done, if any.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether recovery was successful (including no recovery needed).
    /// </summary>
    public bool IsSuccess => Status != ContentRecoveryStatus.Failed;

    /// <summary>
    /// Creates a result indicating no recovery was needed.
    /// </summary>
    public static ContentRecoveryResult NoRecoveryNeeded(string content) => new()
    {
        Status = ContentRecoveryStatus.NoRecoveryNeeded,
        Content = content
    };

    /// <summary>
    /// Creates a result indicating successful full recovery.
    /// </summary>
    public static ContentRecoveryResult Recovered(string content, string description) => new()
    {
        Status = ContentRecoveryStatus.Recovered,
        Content = content,
        Description = description
    };

    /// <summary>
    /// Creates a result indicating partial recovery with data loss.
    /// </summary>
    public static ContentRecoveryResult PartiallyRecovered(string content, string description) => new()
    {
        Status = ContentRecoveryStatus.PartiallyRecovered,
        Content = content,
        Description = description
    };

    /// <summary>
    /// Creates a result indicating recovery failed.
    /// </summary>
    public static ContentRecoveryResult Failed(string reason) => new()
    {
        Status = ContentRecoveryStatus.Failed,
        Content = string.Empty,
        Description = reason
    };
}

/// <summary>
/// Status of content recovery.
/// </summary>
public enum ContentRecoveryStatus
{
    /// <summary>Content was already valid, no recovery needed.</summary>
    NoRecoveryNeeded,

    /// <summary>Content was fully recovered.</summary>
    Recovered,

    /// <summary>Content was partially recovered with some data loss.</summary>
    PartiallyRecovered,

    /// <summary>Recovery failed.</summary>
    Failed
}
