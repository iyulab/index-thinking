using System.Globalization;
using Microsoft.Extensions.AI;

namespace IndexThinking.Reduction;

/// <summary>
/// The text of a function result as the reduction stages measure and mask it: a string as is; content (a list of
/// <see cref="AIContent"/>, as an MCP tool returns when it carries an image) as its text parts with each image named —
/// never as a type name and never as the bytes; anything else via <c>ToString()</c>.
/// </summary>
internal static class ToolResultTexts
{
    public static string Of(object? result) => result switch
    {
        null => string.Empty,
        string text => text,
        AIContent content => Describe(content),
        IEnumerable<AIContent> contents => string.Join("\n", contents.Select(Describe)),
        _ => result.ToString() ?? string.Empty,
    };

    private static string Describe(AIContent content) => content switch
    {
        TextContent text => text.Text ?? string.Empty,
        DataContent data => string.Create(CultureInfo.InvariantCulture,
            $"[{(data.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "image" : "data")} {data.MediaType}, {Size(data.Data.Length)}]"),
        _ => $"[{content.GetType().Name}]",
    };

    private static string Size(long bytes) => bytes < 1024
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes} B")
        : bytes < 1024 * 1024
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB")
            : string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.#} MB");
}
