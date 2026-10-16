using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;

namespace IndexThinking.Tokenization;

/// <summary>
/// Approximate token counter using character-to-token ratios.
/// Used as a fallback when exact tokenization is not available.
/// </summary>
/// <remarks>
/// Each kind of character has its own ratio (<see cref="LanguageRatios"/>); with the defaults the estimate is within a few
/// percent of o200k_base on English, Korean, mixed prose, code and JSON. A message counts everything it carries - text, tool
/// calls and their arguments, tool results, reasoning, images - not only its text.
/// </remarks>
public sealed class ApproximateTokenCounter : IChatMessageTokenCounter
{
    private readonly LanguageRatios _ratios;

    /// <summary>
    /// Overhead tokens added per chat message for role and formatting.
    /// </summary>
    public const int MessageOverhead = 4;

    /// <summary>
    /// What one image in a message counts: a counter that sees no pixels charges a typical image once (providers price
    /// images by size - OpenAI about 765 tokens for 1024 x 1024 at high detail, Anthropic about 1,600 for 1.15 megapixels).
    /// The exact counter charges the same.
    /// </summary>
    public const int ImageTokens = ChatMessageContents.ImageTokens;

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

        var classes = Classify(text);
        if (classes.Total == 0)
            return 0;

        double tokens = 0;
        tokens += classes.Latin / _ratios.English;
        tokens += classes.Korean / _ratios.Korean;
        tokens += classes.Japanese / _ratios.Japanese;
        tokens += classes.Chinese / _ratios.Chinese;
        tokens += classes.Digits / _ratios.Digit;
        tokens += classes.Punctuation / _ratios.Punctuation;
        tokens += classes.WhitespaceRuns / _ratios.WhitespaceRun;
        tokens += classes.Other / _ratios.Default;

        return (int)Math.Ceiling(tokens);
    }

    /// <inheritdoc />
    public int Count(IEnumerable<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return texts.Sum(t => Count(t));
    }

    /// <inheritdoc />
    public int Count(ChatMessage message) => ChatMessageContents.Count(message, Count, MessageOverhead);

    /// <inheritdoc />
    /// <remarks>
    /// Always returns true as this is a fallback counter that can estimate tokens for any model.
    /// </remarks>
    public bool SupportsModel(string modelId) => true;

    /// <inheritdoc />
    /// <remarks>
    /// Always returns true as this counter uses heuristic character-to-token ratios.
    /// </remarks>
#pragma warning disable CA1822 // Interface method override cannot be static
    public bool IsApproximate(string modelId) => true;
#pragma warning restore CA1822

    /// <summary>
    /// Detects the primary language of the text.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>The detected primary language.</returns>
    public static DetectedLanguage DetectLanguage(string text)
    {
        if (string.IsNullOrEmpty(text))
            return DetectedLanguage.Unknown;

        // The language of the words: digits, punctuation and whitespace say nothing about it
        var c = Classify(text);
        var total = c.Latin + c.Korean + c.Japanese + c.Chinese + c.Other;

        if (total == 0)
            return DetectedLanguage.Unknown;

        // Determine primary language (>50% threshold)
        if ((double)c.Korean / total > 0.5)
            return DetectedLanguage.Korean;
        if ((double)c.Japanese / total > 0.5)
            return DetectedLanguage.Japanese;
        if ((double)c.Chinese / total > 0.5)
            return DetectedLanguage.Chinese;
        if ((double)c.Latin / total > 0.5)
            return DetectedLanguage.English;

        return DetectedLanguage.Unknown;
    }

    private readonly record struct CharClasses(
        int Latin, int Korean, int Japanese, int Chinese, int Digits, int Punctuation, int WhitespaceRuns, int Other)
    {
        public int Total => Latin + Korean + Japanese + Chinese + Digits + Punctuation + WhitespaceRuns + Other;
    }

    private static CharClasses Classify(string text)
    {
        int latin = 0, korean = 0, japanese = 0, chinese = 0, digits = 0, punctuation = 0, whitespaceRuns = 0, other = 0;
        var inWhitespace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                // A run of whitespace counts once: a tokenizer merges indentation and line breaks
                if (!inWhitespace)
                {
                    whitespaceRuns++;
                    inWhitespace = true;
                }

                continue;
            }

            inWhitespace = false;
            if (char.IsControl(c))
                continue;
            if (IsKorean(c))
                korean++;
            else if (IsJapanese(c))
                japanese++;
            else if (IsChinese(c))
                chinese++;
            else if (IsLatinScript(c))
                latin++;
            else if (char.IsDigit(c))
                digits++;
            else if (char.IsPunctuation(c) || char.IsSymbol(c))
                punctuation++;
            else
                other++;
        }

        return new CharClasses(latin, korean, japanese, chinese, digits, punctuation, whitespaceRuns, other);
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
