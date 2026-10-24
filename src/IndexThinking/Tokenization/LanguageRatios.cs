using TokenEstimation;

namespace IndexThinking.Tokenization;

/// <summary>
/// Characters per token for each kind of character, used by the approximate counter when no exact tokenizer is
/// available. A text's estimate is the sum over its characters of 1 / ratio of their kind.
/// </summary>
/// <remarks>
/// The defaults are fitted to OpenAI's o200k_base over English chat turns, Korean passages, mixed Korean/English prose,
/// C# source and JSON (2026-10-16): with them each set's estimate is within 2 % of o200k on the sum, against −25 %
/// (Korean), −33 % (JSON) and −13 % (C#) with the earlier letters-only ratios. Other tokenizers differ, most on Korean
/// (cl100k needs about twice as many tokens per Hangul syllable), so treat an estimate as approximate whatever the model.
/// Letters, digits, punctuation and whitespace are counted apart because they tokenize very differently: a Latin word is
/// a few letters per token, while braces, quotes and digits are about one token per character or two.
/// </remarks>
public sealed record LanguageRatios
{
    /// <summary>
    /// Characters per token for Latin-script letters (whitespace, digits and punctuation are counted apart).
    /// </summary>
    public double English { get; init; } = CharTokenRatios.O200k.Latin;

    /// <summary>
    /// Characters per token for Korean text (Hangul).
    /// </summary>
    public double Korean { get; init; } = CharTokenRatios.O200k.Korean;

    /// <summary>
    /// Characters per token for Japanese text (Hiragana, Katakana, Kanji).
    /// </summary>
    public double Japanese { get; init; } = CharTokenRatios.O200k.Japanese;

    /// <summary>
    /// Characters per token for Chinese text (Hanzi).
    /// Chinese characters are often tokenized individually or in pairs.
    /// </summary>
    public double Chinese { get; init; } = CharTokenRatios.O200k.Chinese;

    /// <summary>
    /// Characters per token for decimal digits.
    /// </summary>
    public double Digit { get; init; } = CharTokenRatios.O200k.Digit;

    /// <summary>
    /// Characters per token for punctuation and symbols (braces, quotes, operators) - the bulk of JSON and code.
    /// </summary>
    public double Punctuation { get; init; } = CharTokenRatios.O200k.Punctuation;

    /// <summary>
    /// Whitespace runs per token: a run of spaces, tabs or newlines (an indentation, a line break) counts once, and a
    /// tokenizer merges most runs into a neighbouring token.
    /// </summary>
    public double WhitespaceRun { get; init; } = CharTokenRatios.O200k.WhitespaceRun;

    /// <summary>
    /// Characters per token for any other character (other scripts, emoji).
    /// </summary>
    public double Default { get; init; } = CharTokenRatios.O200k.Other;

    /// <summary>
    /// Gets the default ratios based on empirical tokenization data.
    /// </summary>
    public static LanguageRatios DefaultRatios { get; } = new();

    /// <summary>
    /// Ratios fitted to Gemma's tokenizer (Gemma 4, measured through llama-server on English chat, Korean passages, mixed
    /// Korean/English prose, C# and JSON). It splits digits and punctuation finer than o200k_base: with the default ratios a
    /// JSON tool result is undercounted by about a quarter. Japanese and Chinese keep the defaults (not measured).
    /// </summary>
    public static LanguageRatios Gemma { get; } = From(CharTokenRatios.Gemma);

    /// <summary>
    /// Ratios fitted to Qwen's tokenizer (Qwen 3.5, measured as for <see cref="Gemma"/>). With the default ratios a JSON tool
    /// result is undercounted by about a fifth. Japanese and Chinese keep the defaults (not measured).
    /// </summary>
    public static LanguageRatios Qwen { get; } = From(CharTokenRatios.Qwen);

    /// <summary>
    /// The ratios for <paramref name="modelId"/>: <see cref="Gemma"/> or <see cref="Qwen"/> when the id names that family
    /// (<c>gemma-4-E4B-it</c>, <c>gguf:qwen3-fast</c>, <c>qwen3-8b</c> …), otherwise <see cref="DefaultRatios"/> (fitted to o200k_base,
    /// which OpenAI's models use and other cloud models stay close to).
    /// </summary>
    public static LanguageRatios ForModel(string? modelId)
    {
        if (string.IsNullOrEmpty(modelId))
            return DefaultRatios;
        if (modelId.Contains("gemma", StringComparison.OrdinalIgnoreCase))
            return Gemma;
        if (modelId.Contains("qwen", StringComparison.OrdinalIgnoreCase))
            return Qwen;
        return DefaultRatios;
    }

    private static LanguageRatios From(CharTokenRatios t) => new()
    {
        English = t.Latin, Korean = t.Korean, Japanese = t.Japanese, Chinese = t.Chinese,
        Digit = t.Digit, Punctuation = t.Punctuation, WhitespaceRun = t.WhitespaceRun, Default = t.Other,
    };

    internal CharTokenRatios ToTable() =>
        new(English, Korean, Japanese, Chinese, Digit, Punctuation, WhitespaceRun, Default);

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
