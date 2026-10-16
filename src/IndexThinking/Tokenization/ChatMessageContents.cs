using System.Text.Json;
using Microsoft.Extensions.AI;

namespace IndexThinking.Tokenization;

/// <summary>
/// What a chat message sends to a model, as text a token counter can count: its text, a tool call's name and arguments, a
/// tool result, reasoning text - and a fixed cost per image. The message counters of <see cref="ApproximateTokenCounter"/> and
/// <see cref="TiktokenTokenCounter"/> share it, so a message's count does not depend on which counter the chain picked.
/// </summary>
internal static class ChatMessageContents
{
    /// <summary>
    /// What one image costs. Providers price images by size (OpenAI about 765 tokens for a 1024 x 1024 image at high detail,
    /// Anthropic about 1,600 for 1.15 megapixels); a counter that sees no pixels charges a typical image once.
    /// </summary>
    public const int ImageTokens = 1000;

    /// <summary>Tokens for the structure of each tool call or result (its id, its name field, the role it travels in).</summary>
    public const int ToolContentOverhead = 8;

    public static int Count(ChatMessage message, Func<string, int> countText, int messageOverhead)
    {
        ArgumentNullException.ThrowIfNull(message);

        var tokens = messageOverhead;
        foreach (var content in message.Contents)
        {
            tokens += content switch
            {
                TextReasoningContent reasoning => countText(reasoning.Text ?? string.Empty),
                FunctionCallContent call => ToolContentOverhead + countText(call.Name) + countText(Serialize(call.Arguments)),
                FunctionResultContent result => ToolContentOverhead + CountResult(result.Result, countText),
                _ => CountContent(content, countText),
            };
        }

        return tokens;
    }

    // A tool result may be content itself - a screenshot tool returns an image, an MCP tool a list of text and images
    private static int CountResult(object? result, Func<string, int> countText) => result switch
    {
        AIContent content => CountContent(content, countText),
        IEnumerable<AIContent> contents => contents.Sum(c => CountContent(c, countText)),
        _ => countText(ResultText(result)),
    };

    private static int CountContent(AIContent content, Func<string, int> countText) => content switch
    {
        TextContent text => countText(text.Text ?? string.Empty),
        DataContent data when data.HasTopLevelMediaType("image") => ImageTokens,
        UriContent uri when uri.HasTopLevelMediaType("image") => ImageTokens,
        // An image content type of a library's own (not DataContent) is still an image
        _ when content.GetType().Name.Contains("Image", StringComparison.OrdinalIgnoreCase) => ImageTokens,
        DataContent data => countText($"[data {data.MediaType}, {data.Data.Length} bytes]"),
        _ => 0,
    };

    /// <summary>
    /// What the tool definitions a request offers cost: each function's name, description and parameter schema travel with
    /// every request, often thousands of tokens for a full tool set, though no message carries them.
    /// </summary>
    public static int CountTools(IEnumerable<AITool>? tools, Func<string, int> countText)
    {
        if (tools is null)
        {
            return 0;
        }

        var tokens = 0;
        foreach (var tool in tools)
        {
            tokens += ToolContentOverhead + countText(tool.Name) + countText(tool.Description ?? string.Empty);
            if (tool is AIFunctionDeclaration function)
            {
                tokens += countText(function.JsonSchema.GetRawText());
            }
        }

        return tokens;
    }

    private static string ResultText(object? result) => result switch
    {
        null => string.Empty,
        string text => text,
        JsonElement element => element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText(),
        _ => Serialize(result),
    };

    private static string Serialize(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        try
        {
            return JsonSerializer.Serialize(value, AIJsonUtilities.DefaultOptions.GetTypeInfo(value.GetType()));
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or JsonException)
        {
            // A result type the serializer cannot write still reaches the model as its text
            return value.ToString() ?? string.Empty;
        }
    }
}
