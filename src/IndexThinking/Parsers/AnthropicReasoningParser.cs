using System.Text;
using System.Text.Json;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using IndexThinking.Parsers.Models;
using Microsoft.Extensions.AI;

namespace IndexThinking.Parsers;

/// <summary>
/// Parser for Anthropic Claude Extended Thinking responses.
/// </summary>
/// <remarks>
/// Anthropic's Extended Thinking feature returns:
/// - Thinking blocks with internal reasoning and cryptographic signatures
/// - Redacted thinking blocks for filtered content
/// - Text blocks for the final response
///
/// The signature field must be preserved exactly when passing thinking blocks
/// back to the API for multi-turn conversations with tools.
/// </remarks>
public sealed class AnthropicReasoningParser : IReasoningParser
{
    /// <summary>
    /// Provider family identifier for Anthropic.
    /// </summary>
    public const string Provider = "anthropic";

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

        var contentBlocks = ExtractContentBlocks(response);
        var thinkingBlocks = contentBlocks
            .OfType<AnthropicThinkingBlock>()
            .ToList();

        if (thinkingBlocks.Count == 0)
        {
            return false;
        }

        // Combine all thinking blocks
        var thinkingText = CombineThinkingText(thinkingBlocks);
        var tokenCount = ExtractThinkingTokenCount(response);

        content = new ThinkingContent
        {
            Text = thinkingText,
            TokenCount = tokenCount,
            IsSummarized = false // Anthropic returns full thinking
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

        var contentBlocks = ExtractContentBlocks(response);
        var thinkingBlocks = contentBlocks
            .OfType<AnthropicThinkingBlock>()
            .ToList();

        var redactedBlocks = contentBlocks
            .OfType<AnthropicRedactedThinkingBlock>()
            .ToList();

        if (thinkingBlocks.Count == 0 && redactedBlocks.Count == 0)
        {
            return null;
        }

        // Serialize thinking blocks with signatures for state preservation
        var stateData = new AnthropicThinkingState
        {
            ThinkingBlocks = thinkingBlocks,
            RedactedBlocks = redactedBlocks
        };

        var json = JsonSerializer.Serialize(stateData, JsonOptions);
        var data = Encoding.UTF8.GetBytes(json);

        return new ReasoningState
        {
            Provider = Provider,
            Data = data
        };
    }

    /// <summary>
    /// Attempts to parse thinking content directly from content blocks.
    /// </summary>
    /// <param name="contentBlocks">The content blocks to parse.</param>
    /// <param name="content">The parsed thinking content, if successful.</param>
    /// <returns>True if thinking content was found; otherwise, false.</returns>
    public bool TryParse(IEnumerable<AnthropicContentBlock> contentBlocks, out ThinkingContent? content)
    {
        content = null;

        var thinkingBlocks = contentBlocks
            .OfType<AnthropicThinkingBlock>()
            .ToList();

        if (thinkingBlocks.Count == 0)
        {
            return false;
        }

        content = new ThinkingContent
        {
            Text = CombineThinkingText(thinkingBlocks),
            TokenCount = 0, // Token count not available from blocks alone
            IsSummarized = false
        };

        return true;
    }

    /// <summary>
    /// Extracts reasoning state directly from content blocks.
    /// </summary>
    /// <param name="contentBlocks">The content blocks to extract state from.</param>
    /// <returns>The reasoning state if thinking blocks are present; otherwise, null.</returns>
    public ReasoningState? ExtractState(IEnumerable<AnthropicContentBlock> contentBlocks)
    {
        var blocks = contentBlocks.ToList();
        var thinkingBlocks = blocks.OfType<AnthropicThinkingBlock>().ToList();
        var redactedBlocks = blocks.OfType<AnthropicRedactedThinkingBlock>().ToList();

        if (thinkingBlocks.Count == 0 && redactedBlocks.Count == 0)
        {
            return null;
        }

        var stateData = new AnthropicThinkingState
        {
            ThinkingBlocks = thinkingBlocks,
            RedactedBlocks = redactedBlocks
        };

        var json = JsonSerializer.Serialize(stateData, JsonOptions);

        return new ReasoningState
        {
            Provider = Provider,
            Data = Encoding.UTF8.GetBytes(json)
        };
    }

    /// <summary>
    /// Restores thinking blocks from a previously extracted reasoning state.
    /// </summary>
    /// <param name="state">The reasoning state to restore from.</param>
    /// <returns>The restored thinking state, or null if restoration fails.</returns>
    public static AnthropicThinkingState? RestoreState(ReasoningState state)
    {
        if (state?.Provider != Provider || state.Data is null || state.Data.Length == 0)
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(state.Data);
            return JsonSerializer.Deserialize<AnthropicThinkingState>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if the response contains any redacted thinking blocks.
    /// </summary>
    /// <param name="response">The response to check.</param>
    /// <returns>True if redacted thinking is present; otherwise, false.</returns>
    public bool HasRedactedThinking(ChatResponse response)
    {
        if (response is null)
        {
            return false;
        }

        return ExtractContentBlocks(response)
            .OfType<AnthropicRedactedThinkingBlock>()
            .Any();
    }

    private static IReadOnlyList<AnthropicContentBlock> ExtractContentBlocks(ChatResponse response)
    {
        var result = new List<AnthropicContentBlock>();

        // Try RawRepresentation first
        if (response.RawRepresentation is not null)
        {
            try
            {
                if (response.RawRepresentation is JsonElement jsonElement)
                {
                    var blocks = ParseFromJsonElement(jsonElement);
                    if (blocks.Count > 0)
                    {
                        return blocks;
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
        if (response.AdditionalProperties?.TryGetValue("content", out var content) == true)
        {
            try
            {
                if (content is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    return ParseContentArray(element);
                }
            }
            catch (JsonException)
            {
                // Fall through
            }
        }

        return result;
    }

    private static IReadOnlyList<AnthropicContentBlock> ParseFromJsonElement(JsonElement element)
    {
        // Look for "content" array
        if (element.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            return ParseContentArray(content);
        }

        // If the element itself is an array
        if (element.ValueKind == JsonValueKind.Array)
        {
            return ParseContentArray(element);
        }

        return Array.Empty<AnthropicContentBlock>();
    }

    private static IReadOnlyList<AnthropicContentBlock> ParseFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseFromJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return Array.Empty<AnthropicContentBlock>();
        }
    }

    private static IReadOnlyList<AnthropicContentBlock> ParseContentArray(JsonElement array)
    {
        var result = new List<AnthropicContentBlock>();

        foreach (var item in array.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var typeElement))
            {
                continue;
            }

            var type = typeElement.GetString();
            try
            {
                AnthropicContentBlock? block = type switch
                {
                    "thinking" => ParseThinkingBlock(item),
                    "redacted_thinking" => ParseRedactedBlock(item),
                    "text" => ParseTextBlock(item),
                    _ => null
                };

                if (block is not null)
                {
                    result.Add(block);
                }
            }
            catch (JsonException)
            {
                // Skip malformed blocks
            }
        }

        return result;
    }

    private static AnthropicThinkingBlock? ParseThinkingBlock(JsonElement element)
    {
        if (!element.TryGetProperty("thinking", out var thinking) ||
            !element.TryGetProperty("signature", out var signature))
        {
            return null;
        }

        var thinkingText = thinking.GetString();
        var signatureText = signature.GetString();

        if (string.IsNullOrEmpty(thinkingText) || string.IsNullOrEmpty(signatureText))
        {
            return null;
        }

        return new AnthropicThinkingBlock
        {
            Thinking = thinkingText,
            Signature = signatureText
        };
    }

    private static AnthropicRedactedThinkingBlock? ParseRedactedBlock(JsonElement element)
    {
        if (!element.TryGetProperty("data", out var data))
        {
            return null;
        }

        var dataText = data.GetString();
        if (string.IsNullOrEmpty(dataText))
        {
            return null;
        }

        return new AnthropicRedactedThinkingBlock
        {
            Data = dataText
        };
    }

    private static AnthropicTextBlock? ParseTextBlock(JsonElement element)
    {
        if (!element.TryGetProperty("text", out var text))
        {
            return null;
        }

        var textContent = text.GetString();
        if (textContent is null)
        {
            return null;
        }

        return new AnthropicTextBlock
        {
            Text = textContent
        };
    }

    private static string CombineThinkingText(IReadOnlyList<AnthropicThinkingBlock> blocks)
    {
        if (blocks.Count == 1)
        {
            return blocks[0].Thinking;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }
            builder.Append(blocks[i].Thinking);
        }

        return builder.ToString();
    }

    private static int ExtractThinkingTokenCount(ChatResponse response)
    {
        // Try RawRepresentation first
        if (response.RawRepresentation is JsonElement rawElement &&
            rawElement.TryGetProperty("usage", out var usage) &&
            usage.TryGetProperty("thinking_tokens", out var thinkingTokens) &&
            thinkingTokens.TryGetInt32(out var count))
        {
            return count;
        }

        // Try response AdditionalProperties
        if (response.AdditionalProperties?.TryGetValue("thinking_tokens", out var tokens) == true)
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

/// <summary>
/// Internal state structure for Anthropic thinking preservation.
/// </summary>
public sealed record AnthropicThinkingState
{
    /// <summary>
    /// The thinking blocks with their signatures.
    /// </summary>
    public IReadOnlyList<AnthropicThinkingBlock> ThinkingBlocks { get; init; } = Array.Empty<AnthropicThinkingBlock>();

    /// <summary>
    /// Any redacted thinking blocks.
    /// </summary>
    public IReadOnlyList<AnthropicRedactedThinkingBlock> RedactedBlocks { get; init; } = Array.Empty<AnthropicRedactedThinkingBlock>();
}
