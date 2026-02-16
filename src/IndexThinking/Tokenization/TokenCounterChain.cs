using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;

namespace IndexThinking.Tokenization;

/// <summary>
/// Token counter that tries multiple counters in order until one supports the model.
/// Implements fallback chain: Exact → Encoding-based → Approximate.
/// </summary>
public sealed class TokenCounterChain : ITokenCounter
{
    private readonly List<ITokenCounter> _counters;
    private readonly string? _modelId;

    /// <summary>
    /// Creates a token counter chain with the specified counters.
    /// </summary>
    /// <param name="counters">The counters to try in order.</param>
    /// <exception cref="ArgumentException">Thrown when counters is empty.</exception>
    public TokenCounterChain(IEnumerable<ITokenCounter> counters)
        : this(counters, null)
    {
    }

    private TokenCounterChain(IEnumerable<ITokenCounter> counters, string? modelId)
    {
        var counterList = counters?.ToList() ?? throw new ArgumentNullException(nameof(counters));

        if (counterList.Count == 0)
            throw new ArgumentException("At least one counter is required.", nameof(counters));

        _counters = counterList;
        _modelId = modelId;
    }

    /// <summary>
    /// Creates a new chain configured for the specified model.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>A new chain with the model set.</returns>
    public TokenCounterChain WithModel(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return new TokenCounterChain(_counters, modelId);
    }

    /// <inheritdoc />
    public int Count(string text)
    {
        var counter = GetSupportingCounter();
        return counter.Count(text);
    }

    /// <inheritdoc />
    public int Count(ChatMessage message)
    {
        var counter = GetSupportingCounter();
        return counter.Count(message);
    }

    /// <inheritdoc />
    public bool SupportsModel(string modelId)
    {
        return _counters.Any(c => c.SupportsModel(modelId));
    }

    /// <summary>
    /// Gets the current model ID, if set.
    /// </summary>
    public string? ModelId => _modelId;

    /// <summary>
    /// Gets the counters in this chain.
    /// </summary>
    public IReadOnlyList<ITokenCounter> Counters => _counters;

    private ITokenCounter GetSupportingCounter()
    {
        // If no model is set, use the first counter
        if (_modelId is null)
            return _counters[0];

        // Find the first counter that supports the model
        foreach (var counter in _counters)
        {
            if (counter.SupportsModel(_modelId))
                return counter;
        }

        // This should not happen if the chain includes a fallback counter
        throw new InvalidOperationException(
            $"No counter in the chain supports model: {_modelId}");
    }
}
