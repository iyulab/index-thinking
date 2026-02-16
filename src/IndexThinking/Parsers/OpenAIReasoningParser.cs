using System.Text;
using System.Text.Json;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using IndexThinking.Parsers.Models;
using Microsoft.Extensions.AI;

namespace IndexThinking.Parsers;

/// <summary>
/// Parser for OpenAI reasoning model responses (o1, o3, o4 series).
/// </summary>
/// <remarks>
/// OpenAI's reasoning models return reasoning through the Responses API with:
/// - Reasoning items containing summary and/or encrypted_content
/// - Token usage details including reasoning_tokens
///
/// The encrypted_content is used for stateless multi-turn conversations
/// and Zero Data Retention (ZDR) compliance.
/// </remarks>
public sealed class OpenAIReasoningParser : IReasoningParser
{
    /// <summary>
    /// Provider family identifier for OpenAI.
    /// </summary>
    public const string Provider = "openai";

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

        // Try to extract from RawRepresentation
        var reasoningItem = ExtractReasoningItem(response);
        if (reasoningItem is null)
        {
            return false;
        }

        // Build thinking content from summary
        var summaryText = BuildSummaryText(reasoningItem.Summary);
        if (string.IsNullOrEmpty(summaryText) && string.IsNullOrEmpty(reasoningItem.EncryptedContent))
        {
            return false;
        }

        // Extract token count from usage if available
        var tokenCount = ExtractReasoningTokenCount(response);

        content = new ThinkingContent
        {
            Text = summaryText ?? "[Reasoning content encrypted]",
            TokenCount = tokenCount,
            IsSummarized = !string.IsNullOrEmpty(summaryText)
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

        var reasoningItem = ExtractReasoningItem(response);
        if (reasoningItem?.EncryptedContent is null)
        {
            return null;
        }

        // Store encrypted content as bytes for preservation
        var data = Encoding.UTF8.GetBytes(reasoningItem.EncryptedContent);

        return new ReasoningState
        {
            Provider = Provider,
            Data = data
        };
    }

    /// <summary>
    /// Attempts to parse reasoning content directly from a reasoning item.
    /// </summary>
    /// <param name="reasoningItem">The reasoning item to parse.</param>
    /// <param name="content">The parsed thinking content, if successful.</param>
    /// <returns>True if content was parsed; otherwise, false.</returns>
    public static bool TryParse(OpenAIReasoningItem reasoningItem, out ThinkingContent? content)
    {
        content = null;

        if (reasoningItem is null)
        {
            return false;
        }

        var summaryText = BuildSummaryText(reasoningItem.Summary);
        if (string.IsNullOrEmpty(summaryText) && string.IsNullOrEmpty(reasoningItem.EncryptedContent))
        {
            return false;
        }

        content = new ThinkingContent
        {
            Text = summaryText ?? "[Reasoning content encrypted]",
            TokenCount = 0, // Token count not available from item alone
            IsSummarized = !string.IsNullOrEmpty(summaryText)
        };

        return true;
    }

    /// <summary>
    /// Extracts reasoning state directly from a reasoning item.
    /// </summary>
    /// <param name="reasoningItem">The reasoning item to extract state from.</param>
    /// <returns>The reasoning state if encrypted content is present; otherwise, null.</returns>
    public static ReasoningState? ExtractState(OpenAIReasoningItem reasoningItem)
    {
        if (reasoningItem?.EncryptedContent is null)
        {
            return null;
        }

        return new ReasoningState
        {
            Provider = Provider,
            Data = Encoding.UTF8.GetBytes(reasoningItem.EncryptedContent)
        };
    }

    private static OpenAIReasoningItem? ExtractReasoningItem(ChatResponse response)
    {
        // Try RawRepresentation first
        if (response.RawRepresentation is not null)
        {
            try
            {
                // Check if it's a JsonElement
                if (response.RawRepresentation is JsonElement jsonElement)
                {
                    return ParseFromJsonElement(jsonElement);
                }

                // Try to serialize and re-parse if it's an object
                var json = JsonSerializer.Serialize(response.RawRepresentation, JsonOptions);
                return ParseFromJson(json);
            }
            catch (JsonException)
            {
                // Fall through to other extraction methods
            }
        }

        // Try AdditionalProperties
        if (response.AdditionalProperties?.TryGetValue("reasoning", out var reasoning) == true)
        {
            try
            {
                if (reasoning is JsonElement element)
                {
                    return ParseReasoningItem(element);
                }

                if (reasoning is OpenAIReasoningItem item)
                {
                    return item;
                }
            }
            catch (JsonException)
            {
                // Fall through
            }
        }

        return null;
    }

    private static OpenAIReasoningItem? ParseFromJsonElement(JsonElement element)
    {
        // Look for "output" array containing reasoning items
        if (element.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var type) &&
                    type.GetString()?.Equals("reasoning", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return ParseReasoningItem(item);
                }
            }
        }

        // Check if the element itself is a reasoning item
        if (element.TryGetProperty("type", out var elementType) &&
            elementType.GetString()?.Equals("reasoning", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ParseReasoningItem(element);
        }

        return null;
    }

    private static OpenAIReasoningItem? ParseFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseFromJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static OpenAIReasoningItem? ParseReasoningItem(JsonElement element)
    {
        try
        {
            return element.Deserialize<OpenAIReasoningItem>(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? BuildSummaryText(IReadOnlyList<OpenAIReasoningSummary>? summaries)
    {
        if (summaries is null || summaries.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var summary in summaries)
        {
            if (!string.IsNullOrEmpty(summary.Text))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }
                builder.Append(summary.Text);
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    private static int ExtractReasoningTokenCount(ChatResponse response)
    {
        // Try RawRepresentation for reasoning tokens
        if (response.RawRepresentation is JsonElement rawElement)
        {
            if (rawElement.TryGetProperty("usage", out var usage) &&
                usage.TryGetProperty("output_tokens_details", out var details) &&
                details.TryGetProperty("reasoning_tokens", out var reasoningTokens) &&
                reasoningTokens.TryGetInt32(out var count))
            {
                return count;
            }
        }

        // Try AdditionalProperties on response
        if (response.AdditionalProperties?.TryGetValue("reasoning_tokens", out var tokens) == true)
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
