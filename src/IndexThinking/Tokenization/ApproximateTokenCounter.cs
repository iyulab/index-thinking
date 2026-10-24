using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;
using TokenEstimation;

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
    private readonly CharTokenRatios _table;

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
    /// An approximate counter with the ratios for <paramref name="modelId"/>'s tokenizer family (<see cref="LanguageRatios.ForModel"/>):
    /// fitted to Gemma or Qwen for those local models, o200k_base otherwise.
    /// </summary>
    public static ApproximateTokenCounter ForModel(string? modelId) => new(LanguageRatios.ForModel(modelId));

    /// <summary>
    /// Creates an approximate counter with custom language ratios.
    /// </summary>
    /// <param name="ratios">Custom character-to-token ratios.</param>
    public ApproximateTokenCounter(LanguageRatios ratios)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        _table = ratios.ToTable();
    }

    /// <inheritdoc />
    public int Count(string text) => CharTokenEstimator.Count(text, _table);

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
        var c = CharTokenEstimator.Classify(text);
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
}
