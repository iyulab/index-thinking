namespace IndexThinking.Tokenization;

/// <summary>
/// Character-to-token ratios for different languages.
/// Used by approximate token counters when exact tokenization is not available.
/// </summary>
/// <remarks>
/// These ratios are based on empirical observations of tokenization behavior:
/// - English and Latin-script languages: ~4 characters per token
/// - CJK languages (Chinese, Japanese, Korean): Higher token density
/// - Mixed content uses weighted averages
/// </remarks>
public sealed record LanguageRatios
{
    /// <summary>
    /// Characters per token for English and Latin-script text.
    /// </summary>
    public double English { get; init; } = 4.0;

    /// <summary>
    /// Characters per token for Korean text (Hangul).
    /// Korean typically requires more tokens due to syllable-based encoding.
    /// </summary>
    public double Korean { get; init; } = 1.5;

    /// <summary>
    /// Characters per token for Japanese text (Hiragana, Katakana, Kanji).
    /// </summary>
    public double Japanese { get; init; } = 1.5;

    /// <summary>
    /// Characters per token for Chinese text (Hanzi).
    /// Chinese characters are often tokenized individually or in pairs.
    /// </summary>
    public double Chinese { get; init; } = 1.2;

    /// <summary>
    /// Default ratio for unknown or mixed language text.
    /// Conservative estimate to avoid underestimating token counts.
    /// </summary>
    public double Default { get; init; } = 3.5;

    /// <summary>
    /// Gets the default ratios based on empirical tokenization data.
    /// </summary>
    public static LanguageRatios DefaultRatios { get; } = new();

    /// <summary>
    /// Gets the ratio for a detected language.
    /// </summary>
    /// <param name="language">The detected language.</param>
    /// <returns>The character-to-token ratio for the language.</returns>
    public double GetRatio(DetectedLanguage language)
    {
        return language switch
        {
            DetectedLanguage.English => English,
            DetectedLanguage.Korean => Korean,
            DetectedLanguage.Japanese => Japanese,
            DetectedLanguage.Chinese => Chinese,
            _ => Default
        };
    }
}

/// <summary>
/// Language detection result for token estimation.
/// </summary>
public enum DetectedLanguage
{
    /// <summary>Unknown or mixed language.</summary>
    Unknown,

    /// <summary>English or Latin-script text.</summary>
    English,

    /// <summary>Korean text (Hangul).</summary>
    Korean,

    /// <summary>Japanese text.</summary>
    Japanese,

    /// <summary>Chinese text.</summary>
    Chinese
}
