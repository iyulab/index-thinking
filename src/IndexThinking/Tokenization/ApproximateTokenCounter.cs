using System.Globalization;
using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;

namespace IndexThinking.Tokenization;

/// <summary>
/// Approximate token counter using character-to-token ratios.
/// Used as a fallback when exact tokenization is not available.
/// </summary>
/// <remarks>
/// This counter provides estimates that are typically within ±20% of actual token counts.
/// It uses language detection to apply appropriate ratios for different scripts.
/// </remarks>
public sealed class ApproximateTokenCounter : ITokenCounter
{
    private readonly LanguageRatios _ratios;

    /// <summary>
    /// Overhead tokens added per chat message for role and formatting.
    /// </summary>
    public const int MessageOverhead = 4;

    /// <summary>
    /// Creates an approximate counter with default language ratios.
    /// </summary>
    public ApproximateTokenCounter()
        : this(LanguageRatios.DefaultRatios)
    {
    }

    /// <summary>
    /// Creates an approximate counter with custom language ratios.
    /// </summary>
    /// <param name="ratios">Custom character-to-token ratios.</param>
    public ApproximateTokenCounter(LanguageRatios ratios)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        _ratios = ratios;
    }

    /// <inheritdoc />
    public int Count(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var (englishChars, koreanChars, japaneseChars, chineseChars, otherChars) = AnalyzeText(text);
        var totalChars = englishChars + koreanChars + japaneseChars + chineseChars + otherChars;

        if (totalChars == 0)
            return 0;

        // Calculate weighted token count based on character distribution
        double tokens = 0;
        tokens += englishChars / _ratios.English;
        tokens += koreanChars / _ratios.Korean;
        tokens += japaneseChars / _ratios.Japanese;
        tokens += chineseChars / _ratios.Chinese;
        tokens += otherChars / _ratios.Default;

        return (int)Math.Ceiling(tokens);
    }

    /// <inheritdoc />
    public int Count(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var textTokens = 0;

        foreach (var content in message.Contents)
        {
            if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
            {
                textTokens += Count(textContent.Text);
            }
        }

        return textTokens + MessageOverhead;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always returns true as this is a fallback counter that can estimate tokens for any model.
    /// </remarks>
    public bool SupportsModel(string modelId) => true;

    /// <summary>
    /// Detects the primary language of the text.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>The detected primary language.</returns>
    public static DetectedLanguage DetectLanguage(string text)
    {
        if (string.IsNullOrEmpty(text))
            return DetectedLanguage.Unknown;

        var (english, korean, japanese, chinese, other) = AnalyzeText(text);
        var total = english + korean + japanese + chinese + other;

        if (total == 0)
            return DetectedLanguage.Unknown;

        // Determine primary language (>50% threshold)
        if ((double)korean / total > 0.5)
            return DetectedLanguage.Korean;
        if ((double)japanese / total > 0.5)
            return DetectedLanguage.Japanese;
        if ((double)chinese / total > 0.5)
            return DetectedLanguage.Chinese;
        if ((double)english / total > 0.5)
            return DetectedLanguage.English;

        return DetectedLanguage.Unknown;
    }

    private static (int english, int korean, int japanese, int chinese, int other) AnalyzeText(string text)
    {
        int english = 0, korean = 0, japanese = 0, chinese = 0, other = 0;

        foreach (var c in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            // Skip whitespace and control characters
            if (category is UnicodeCategory.SpaceSeparator or
                UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator or
                UnicodeCategory.Control)
            {
                continue;
            }

            if (IsKorean(c))
                korean++;
            else if (IsJapanese(c))
                japanese++;
            else if (IsChinese(c))
                chinese++;
            else if (IsLatinScript(c))
                english++;
            else
                other++;
        }

        return (english, korean, japanese, chinese, other);
    }

    private static bool IsLatinScript(char c)
    {
        // Basic Latin, Latin-1 Supplement, Latin Extended
        return c <= '\u024F' && char.IsLetter(c);
    }

    private static bool IsKorean(char c)
    {
        // Hangul Syllables: U+AC00-U+D7AF
        // Hangul Jamo: U+1100-U+11FF
        // Hangul Compatibility Jamo: U+3130-U+318F
        return (c >= '\uAC00' && c <= '\uD7AF') ||
               (c >= '\u1100' && c <= '\u11FF') ||
               (c >= '\u3130' && c <= '\u318F');
    }

    private static bool IsJapanese(char c)
    {
        // Hiragana: U+3040-U+309F
        // Katakana: U+30A0-U+30FF
        // Note: Kanji overlaps with Chinese
        return (c >= '\u3040' && c <= '\u309F') ||
               (c >= '\u30A0' && c <= '\u30FF');
    }

    private static bool IsChinese(char c)
    {
        // CJK Unified Ideographs: U+4E00-U+9FFF
        // CJK Extension A: U+3400-U+4DBF
        return (c >= '\u4E00' && c <= '\u9FFF') ||
               (c >= '\u3400' && c <= '\u4DBF');
    }
}
