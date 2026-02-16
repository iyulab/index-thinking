using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using IndexThinking.Parsers.Models;

namespace IndexThinking.Parsers;

/// <summary>
/// Parser for open-source reasoning model responses.
/// Supports DeepSeek, Qwen, GLM, and vLLM-served models.
/// </summary>
/// <remarks>
/// <para>
/// Open-source reasoning models typically use one of two approaches:
/// 1. Structured API field: <c>reasoning_content</c> or <c>reasoning</c> field
/// 2. Text markers: <c>&lt;think&gt;...&lt;/think&gt;</c> tags in content
/// </para>
/// <para>
/// This parser supports both approaches and can extract reasoning from
/// either structured fields or by parsing think tags from the content.
/// </para>
/// </remarks>
public sealed partial class OpenSourceReasoningParser : IReasoningParser
{
    /// <summary>
    /// Provider family identifier for open-source models.
    /// </summary>
    public const string Provider = "opensource";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Regex pattern for extracting content within <think>...</think> tags
    [GeneratedRegex(@"<think>(.*?)</think>", RegexOptions.Singleline)]
    private static partial Regex ThinkTagsRegex();

    private readonly DeepSeekThinkingConfig _config;

    /// <summary>
    /// Creates a new parser with default configuration.
    /// </summary>
    public OpenSourceReasoningParser() : this(new DeepSeekThinkingConfig())
    {
    }

    /// <summary>
    /// Creates a new parser with the specified configuration.
    /// </summary>
    /// <param name="config">Thinking configuration.</param>
    public OpenSourceReasoningParser(DeepSeekThinkingConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

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

        // Try to extract from structured fields first
        var reasoningContent = ExtractReasoningContent(response);

        if (string.IsNullOrEmpty(reasoningContent))
        {
            // Fall back to parsing think tags from content
            reasoningContent = ExtractFromThinkTags(response);
        }

        if (string.IsNullOrEmpty(reasoningContent))
        {
            return false;
        }

        var tokenCount = ExtractReasoningTokenCount(response);

        content = new ThinkingContent
        {
            Text = reasoningContent,
            TokenCount = tokenCount,
            IsSummarized = false // Open-source models return full reasoning
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

        // Extract reasoning content for state preservation
        var reasoningContent = ExtractReasoningContent(response);

        if (string.IsNullOrEmpty(reasoningContent))
        {
            reasoningContent = ExtractFromThinkTags(response);
        }

        if (string.IsNullOrEmpty(reasoningContent))
        {
            return null;
        }

        // Store reasoning content as bytes for preservation
        // Note: Unlike OpenAI/Anthropic/Gemini, open-source models don't have
        // encrypted state or signatures. We store the raw reasoning content.
        var data = Encoding.UTF8.GetBytes(reasoningContent);

        return new ReasoningState
        {
            Provider = Provider,
            Data = data
        };
    }

    /// <summary>
    /// Attempts to parse thinking content directly from a message.
    /// </summary>
    /// <param name="message">The message to parse.</param>
    /// <param name="content">The parsed thinking content, if successful.</param>
    /// <returns>True if thinking content was found; otherwise, false.</returns>
    public bool TryParse(OpenSourceReasoningMessage message, out ThinkingContent? content)
    {
        content = null;

        if (message is null)
        {
            return false;
        }

        var reasoningContent = message.ReasoningContent ?? message.Reasoning;

        if (string.IsNullOrEmpty(reasoningContent))
        {
            // Try to extract from think tags in content
            reasoningContent = ExtractThinkTagContent(message.Content);
        }

        if (string.IsNullOrEmpty(reasoningContent))
        {
            return false;
        }

        content = new ThinkingContent
        {
            Text = reasoningContent,
            TokenCount = 0, // Token count not available from message alone
            IsSummarized = false
        };

        return true;
    }

    /// <summary>
    /// Extracts reasoning state directly from a message.
    /// </summary>
    /// <param name="message">The message to extract state from.</param>
    /// <returns>The reasoning state if reasoning content is present; otherwise, null.</returns>
    public ReasoningState? ExtractState(OpenSourceReasoningMessage message)
    {
        if (message is null)
        {
            return null;
        }

        var reasoningContent = message.ReasoningContent ?? message.Reasoning;

        if (string.IsNullOrEmpty(reasoningContent))
        {
            reasoningContent = ExtractThinkTagContent(message.Content);
        }

        if (string.IsNullOrEmpty(reasoningContent))
        {
            return null;
        }

        return new ReasoningState
        {
            Provider = Provider,
            Data = Encoding.UTF8.GetBytes(reasoningContent)
        };
    }

    /// <summary>
    /// Restores the reasoning content from a previously extracted state.
    /// </summary>
    /// <param name="state">The reasoning state to restore from.</param>
    /// <returns>The reasoning content string, or null if restoration fails.</returns>
    public static string? RestoreReasoningContent(ReasoningState state)
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
    /// Extracts the content without think tags from a response.
    /// Useful for getting the final answer without reasoning.
    /// </summary>
    /// <param name="text">The text potentially containing think tags.</param>
    /// <returns>The text with think tags and their content removed.</returns>
    public static string StripThinkTags(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return ThinkTagsRegex().Replace(text, "").Trim();
    }

    /// <summary>
    /// Checks if the response has reasoning content (either structured or tagged).
    /// </summary>
    /// <param name="response">The response to check.</param>
    /// <returns>True if reasoning content is present; otherwise, false.</returns>
    public bool HasReasoningContent(ChatResponse response)
    {
        if (response is null)
        {
            return false;
        }

        var reasoningContent = ExtractReasoningContent(response);
        if (!string.IsNullOrEmpty(reasoningContent))
        {
            return true;
        }

        var tagContent = ExtractFromThinkTags(response);
        return !string.IsNullOrEmpty(tagContent);
    }

    private static string? ExtractReasoningContent(ChatResponse response)
    {
        // Try RawRepresentation first
        if (response.RawRepresentation is not null)
        {
            try
            {
                if (response.RawRepresentation is JsonElement jsonElement)
                {
                    return ParseReasoningFromJson(jsonElement);
                }

                // Try to serialize and re-parse
                var json = JsonSerializer.Serialize(response.RawRepresentation, JsonOptions);
                using var doc = JsonDocument.Parse(json);
                return ParseReasoningFromJson(doc.RootElement);
            }
            catch (JsonException)
            {
                // Fall through
            }
        }

        // Try AdditionalProperties
        if (response.AdditionalProperties is not null)
        {
            // Check for direct reasoning_content
            if (response.AdditionalProperties.TryGetValue("reasoning_content", out var reasoningContent) &&
                reasoningContent is not null)
            {
                return GetStringValue(reasoningContent);
            }

            // Check for reasoning field
            if (response.AdditionalProperties.TryGetValue("reasoning", out var reasoning) &&
                reasoning is not null)
            {
                return GetStringValue(reasoning);
            }
        }

        return null;
    }

    private static string? ParseReasoningFromJson(JsonElement element)
    {
        // OpenAI-compatible format: choices[0].message.reasoning_content
        if (element.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message))
                {
                    // Try reasoning_content first
                    if (message.TryGetProperty("reasoning_content", out var reasoningContent))
                    {
                        var content = reasoningContent.GetString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            return content;
                        }
                    }

                    // Try reasoning (vLLM format)
                    if (message.TryGetProperty("reasoning", out var reasoning))
                    {
                        var content = reasoning.GetString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            return content;
                        }
                    }
                }

                // Check delta for streaming
                if (choice.TryGetProperty("delta", out var delta))
                {
                    if (delta.TryGetProperty("reasoning_content", out var deltaReasoning))
                    {
                        var content = deltaReasoning.GetString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            return content;
                        }
                    }

                    if (delta.TryGetProperty("reasoning", out var deltaR))
                    {
                        var content = deltaR.GetString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            return content;
                        }
                    }
                }
            }
        }

        // Direct reasoning_content at root level
        if (element.TryGetProperty("reasoning_content", out var rootReasoning))
        {
            return rootReasoning.GetString();
        }

        if (element.TryGetProperty("reasoning", out var rootR))
        {
            return rootR.GetString();
        }

        return null;
    }

    private string? ExtractFromThinkTags(ChatResponse response)
    {
        // Get the response text content
        string? textContent = null;

        // Try to get from Messages
        if (response.Messages.Count > 0)
        {
            foreach (var message in response.Messages)
            {
                foreach (var item in message.Contents)
                {
                    if (item is TextContent textItem)
                    {
                        textContent = textItem.Text;
                        if (!string.IsNullOrEmpty(textContent))
                        {
                            break;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(textContent))
                {
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(textContent))
        {
            // Try RawRepresentation
            if (response.RawRepresentation is JsonElement element)
            {
                textContent = ExtractTextFromJson(element);
            }
        }

        if (string.IsNullOrEmpty(textContent))
        {
            return null;
        }

        return ExtractThinkTagContent(textContent);
    }

    private string? ExtractThinkTagContent(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // Look for configured start/end tokens
        var startIndex = text.IndexOf(_config.StartToken, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return null;
        }

        var contentStart = startIndex + _config.StartToken.Length;
        var endIndex = text.IndexOf(_config.EndToken, contentStart, StringComparison.Ordinal);

        if (endIndex < 0)
        {
            // No end token - take everything after start token
            return text[contentStart..].Trim();
        }

        return text[contentStart..endIndex].Trim();
    }

    private static string? ExtractTextFromJson(JsonElement element)
    {
        // OpenAI-compatible: choices[0].message.content
        if (element.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    return content.GetString();
                }
            }
        }

        // Direct content
        if (element.TryGetProperty("content", out var directContent))
        {
            return directContent.GetString();
        }

        return null;
    }

    private static int ExtractReasoningTokenCount(ChatResponse response)
    {
        // Try RawRepresentation first
        if (response.RawRepresentation is JsonElement rawElement)
        {
            if (rawElement.TryGetProperty("usage", out var usage))
            {
                // Check completion_tokens_details.reasoning_tokens
                if (usage.TryGetProperty("completion_tokens_details", out var details) &&
                    details.TryGetProperty("reasoning_tokens", out var reasoningTokens) &&
                    reasoningTokens.TryGetInt32(out var count))
                {
                    return count;
                }

                // DeepSeek-specific: reasoning_tokens at usage level
                if (usage.TryGetProperty("reasoning_tokens", out var directTokens) &&
                    directTokens.TryGetInt32(out var directCount))
                {
                    return directCount;
                }
            }
        }

        // Try response AdditionalProperties
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

    private static string? GetStringValue(object value)
    {
        return value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
            _ => null
        };
    }
}
