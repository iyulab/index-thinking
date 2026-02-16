using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using IndexThinking.Parsers.Models;

namespace IndexThinking.Parsers;

/// <summary>
/// Parser for Google Gemini reasoning responses.
/// Handles both native Gemini API and OpenAI-compatible endpoint formats.
/// </summary>
/// <remarks>
/// <para>
/// Gemini supports thinking/reasoning mode that outputs thought content and
/// thought signatures for multi-turn preservation.
/// </para>
/// <para>
/// For Gemini 3 models, thought signatures are mandatory during function calling.
/// For Gemini 2.5 models, thought signatures are optional but recommended.
/// </para>
/// </remarks>
public sealed class GeminiReasoningParser : IReasoningParser
{
    /// <summary>
    /// Provider family identifier for Gemini.
    /// </summary>
    public const string Provider = "gemini";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public string ProviderFamily => Provider;

    /// <inheritdoc />
    public bool TryParse(ChatResponse response, out ThinkingContent? content)
    {
        content = null;

        if (response is null)
        {
            return false;
        }

        var parts = ExtractContentParts(response);
        var thoughtParts = parts.Where(p => !string.IsNullOrEmpty(p.Thought)).ToList();

        if (thoughtParts.Count == 0)
        {
            return false;
        }

        // Combine all thought content
        var thoughtText = CombineThoughtText(thoughtParts);
        var tokenCount = ExtractThinkingTokenCount(response);

        content = new ThinkingContent
        {
            Text = thoughtText,
            TokenCount = tokenCount,
            IsSummarized = false // Gemini returns full thinking (unless thinking level is minimal)
        };

        return true;
    }

    /// <inheritdoc />
    public ReasoningState? ExtractState(ChatResponse response)
    {
        if (response is null)
        {
            return null;
        }

        var parts = ExtractContentParts(response);
        var signature = ExtractThoughtSignature(parts);

        if (string.IsNullOrEmpty(signature))
        {
            return null;
        }

        // Store signature as bytes for preservation
        var data = Encoding.UTF8.GetBytes(signature);

        return new ReasoningState
        {
            Provider = Provider,
            Data = data
        };
    }

    /// <summary>
    /// Attempts to parse thinking content directly from content parts.
    /// </summary>
    /// <param name="parts">The content parts to parse.</param>
    /// <param name="content">The parsed thinking content, if successful.</param>
    /// <returns>True if thinking content was found; otherwise, false.</returns>
    public static bool TryParse(IEnumerable<GeminiContentPart> parts, out ThinkingContent? content)
    {
        content = null;

        var partsList = parts.ToList();
        var thoughtParts = partsList.Where(p => !string.IsNullOrEmpty(p.Thought)).ToList();

        if (thoughtParts.Count == 0)
        {
            return false;
        }

        content = new ThinkingContent
        {
            Text = CombineThoughtText(thoughtParts),
            TokenCount = 0, // Token count not available from parts alone
            IsSummarized = false
        };

        return true;
    }

    /// <summary>
    /// Extracts reasoning state directly from content parts.
    /// </summary>
    /// <param name="parts">The content parts to extract state from.</param>
    /// <returns>The reasoning state if thought signature is present; otherwise, null.</returns>
    public static ReasoningState? ExtractState(IEnumerable<GeminiContentPart> parts)
    {
        var signature = ExtractThoughtSignature(parts.ToList());

        if (string.IsNullOrEmpty(signature))
        {
            return null;
        }

        return new ReasoningState
        {
            Provider = Provider,
            Data = Encoding.UTF8.GetBytes(signature)
        };
    }

    /// <summary>
    /// Restores the thought signature from a previously extracted reasoning state.
    /// </summary>
    /// <param name="state">The reasoning state to restore from.</param>
    /// <returns>The thought signature string, or null if restoration fails.</returns>
    public static string? RestoreThoughtSignature(ReasoningState state)
    {
        if (state?.Provider != Provider || state.Data is null || state.Data.Length == 0)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(state.Data);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if the response contains a thought signature (required for Gemini 3 function calling).
    /// </summary>
    /// <param name="response">The response to check.</param>
    /// <returns>True if a thought signature is present; otherwise, false.</returns>
    public static bool HasThoughtSignature(ChatResponse response)
    {
        if (response is null)
        {
            return false;
        }

        var parts = ExtractContentParts(response);
        var signature = ExtractThoughtSignature(parts);
        return !string.IsNullOrEmpty(signature);
    }

    private static IReadOnlyList<GeminiContentPart> ExtractContentParts(ChatResponse response)
    {
        var result = new List<GeminiContentPart>();

        // Try RawRepresentation first
        if (response.RawRepresentation is not null)
        {
            try
            {
                if (response.RawRepresentation is JsonElement jsonElement)
                {
                    var parsedParts = ParseFromJsonElement(jsonElement);
                    if (parsedParts.Count > 0)
                    {
                        return parsedParts;
                    }
                }

                // Try to serialize and re-parse
                var json = JsonSerializer.Serialize(response.RawRepresentation, JsonOptions);
                var parsed = ParseFromJson(json);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Fall through
            }
        }

        // Try AdditionalProperties
        if (response.AdditionalProperties?.TryGetValue("candidates", out var candidates) == true)
        {
            try
            {
                if (candidates is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    return ParseCandidatesArray(element);
                }
            }
            catch (JsonException)
            {
                // Fall through
            }
        }

        // Try content parts directly from AdditionalProperties
        if (response.AdditionalProperties?.TryGetValue("parts", out var parts) == true)
        {
            try
            {
                if (parts is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    return ParsePartsArray(element);
                }
            }
            catch (JsonException)
            {
                // Fall through
            }
        }

        return result;
    }

    private static IReadOnlyList<GeminiContentPart> ParseFromJsonElement(JsonElement element)
    {
        // Look for "candidates" array (Gemini native format)
        if (element.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            return ParseCandidatesArray(candidates);
        }

        // Look for "content" with "parts" (simplified format)
        if (element.TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            return ParsePartsArray(parts);
        }

        // Look for direct "parts" array
        if (element.TryGetProperty("parts", out var directParts) && directParts.ValueKind == JsonValueKind.Array)
        {
            return ParsePartsArray(directParts);
        }

        // OpenAI-compatible format: check for choices array
        if (element.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            return ParseOpenAICompatibleFormat(choices);
        }

        return Array.Empty<GeminiContentPart>();
    }

    private static IReadOnlyList<GeminiContentPart> ParseFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseFromJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return Array.Empty<GeminiContentPart>();
        }
    }

    private static List<GeminiContentPart> ParseCandidatesArray(JsonElement candidates)
    {
        var result = new List<GeminiContentPart>();

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (candidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                result.AddRange(ParsePartsArray(parts));
            }
        }

        return result;
    }

    private static List<GeminiContentPart> ParsePartsArray(JsonElement parts)
    {
        var result = new List<GeminiContentPart>();

        foreach (var part in parts.EnumerateArray())
        {
            try
            {
                var contentPart = part.Deserialize<GeminiContentPart>(JsonOptions);
                if (contentPart is not null)
                {
                    result.Add(contentPart);
                }
            }
            catch (JsonException)
            {
                // Skip malformed parts
            }
        }

        return result;
    }

    private static List<GeminiContentPart> ParseOpenAICompatibleFormat(JsonElement choices)
    {
        var result = new List<GeminiContentPart>();

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message))
            {
                var contentPart = new GeminiContentPart();

                // Extract text content
                if (message.TryGetProperty("content", out var content))
                {
                    contentPart.Text = content.GetString();
                }

                // Extract extra_content.google.thought_signature
                if (message.TryGetProperty("extra_content", out var extraContent))
                {
                    contentPart.ExtraContent = extraContent.Deserialize<GeminiExtraContent>(JsonOptions);
                }

                result.Add(contentPart);
            }
        }

        return result;
    }

    private static string? ExtractThoughtSignature(IReadOnlyList<GeminiContentPart> parts)
    {
        foreach (var part in parts)
        {
            // Check direct thoughtSignature field (native Gemini API)
            if (!string.IsNullOrEmpty(part.ThoughtSignature))
            {
                return part.ThoughtSignature;
            }

            // Check extra_content.google.thought_signature (OpenAI-compatible)
            var signature = part.ExtraContent?.Google?.ThoughtSignature;
            if (!string.IsNullOrEmpty(signature))
            {
                return signature;
            }
        }

        return null;
    }

    private static string CombineThoughtText(List<GeminiContentPart> parts)
    {
        if (parts.Count == 1)
        {
            return parts[0].Thought!;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }
            builder.Append(parts[i].Thought);
        }

        return builder.ToString();
    }

    private static int ExtractThinkingTokenCount(ChatResponse response)
    {
        // Try RawRepresentation first (native Gemini format)
        if (response.RawRepresentation is JsonElement rawElement &&
            rawElement.ValueKind == JsonValueKind.Object)
        {
            // Native Gemini: usageMetadata.thoughtsTokenCount
            if (rawElement.TryGetProperty("usageMetadata", out var usage) &&
                usage.ValueKind == JsonValueKind.Object &&
                usage.TryGetProperty("thoughtsTokenCount", out var thoughtsTokens) &&
                thoughtsTokens.TryGetInt32(out var count))
            {
                return count;
            }

            // OpenAI-compatible: usage.completion_tokens_details.reasoning_tokens
            if (rawElement.TryGetProperty("usage", out var oaiUsage) &&
                oaiUsage.ValueKind == JsonValueKind.Object)
            {
                if (oaiUsage.TryGetProperty("completion_tokens_details", out var details) &&
                    details.ValueKind == JsonValueKind.Object &&
                    details.TryGetProperty("reasoning_tokens", out var reasoningTokens) &&
                    reasoningTokens.TryGetInt32(out var rCount))
                {
                    return rCount;
                }
            }
        }

        // Try response AdditionalProperties
        if (response.AdditionalProperties?.TryGetValue("thoughtsTokenCount", out var tokens) == true ||
            response.AdditionalProperties?.TryGetValue("thoughts_token_count", out tokens) == true)
        {
            if (tokens is int intTokens) return intTokens;
            if (tokens is long longTokens) return (int)longTokens;
            if (tokens is JsonElement element && element.TryGetInt32(out var jsonTokens))
            {
                return jsonTokens;
            }
        }

        return 0;
    }
}
