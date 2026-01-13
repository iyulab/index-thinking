using IndexThinking.Abstractions;

namespace IndexThinking.Tokenization;

/// <summary>
/// Factory for creating appropriate token counters based on model.
/// </summary>
public sealed class TokenCounterFactory
{
    private readonly LanguageRatios _approximateRatios;

    /// <summary>
    /// Creates a factory with default settings.
    /// </summary>
    public TokenCounterFactory()
        : this(LanguageRatios.DefaultRatios)
    {
    }

    /// <summary>
    /// Creates a factory with custom approximate counting ratios.
    /// </summary>
    /// <param name="approximateRatios">Custom language ratios for approximate counting.</param>
    public TokenCounterFactory(LanguageRatios approximateRatios)
    {
        ArgumentNullException.ThrowIfNull(approximateRatios);
        _approximateRatios = approximateRatios;
    }

    /// <summary>
    /// Creates a token counter optimized for the specified model.
    /// </summary>
    /// <param name="modelId">The model identifier (e.g., "gpt-4o", "claude-3").</param>
    /// <returns>A token counter configured for the model.</returns>
    /// <remarks>
    /// For OpenAI models, returns a chain with TiktokenTokenCounter first.
    /// For other models, returns an ApproximateTokenCounter.
    /// </remarks>
    public ITokenCounter Create(string? modelId = null)
    {
        var counters = new List<ITokenCounter>();

        // Add Tiktoken counter if model is OpenAI or unknown (default to OpenAI)
        if (modelId is null || ModelEncodingRegistry.IsOpenAIModel(modelId))
        {
            var encoding = modelId is not null
                ? ModelEncodingRegistry.GetEncoding(modelId)
                : ModelEncodingRegistry.O200kBase; // Default to latest encoding

            if (encoding is not null)
            {
                try
                {
                    counters.Add(new TiktokenTokenCounter(encoding));
                }
                catch
                {
                    // If tokenizer creation fails, fall through to approximate
                }
            }
        }

        // Always add approximate counter as final fallback
        counters.Add(new ApproximateTokenCounter(_approximateRatios));

        var chain = new TokenCounterChain(counters);
        return modelId is not null ? chain.WithModel(modelId) : chain;
    }

    /// <summary>
    /// Creates a default token counter chain suitable for most use cases.
    /// </summary>
    /// <returns>A token counter chain with both exact and approximate counters.</returns>
    public ITokenCounter CreateDefault()
    {
        return Create(null);
    }

    /// <summary>
    /// Creates an approximate-only counter for non-OpenAI models.
    /// </summary>
    /// <returns>An approximate token counter.</returns>
    public ITokenCounter CreateApproximate()
    {
        return new ApproximateTokenCounter(_approximateRatios);
    }

    /// <summary>
    /// Gets or creates a shared default factory instance.
    /// </summary>
    public static TokenCounterFactory Default { get; } = new();
}
