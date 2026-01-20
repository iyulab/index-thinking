using System.Diagnostics.CodeAnalysis;
using IndexThinking.Abstractions;

namespace IndexThinking.Modifiers;

/// <summary>
/// Registry for reasoning request modifiers, mapping providers and model IDs to appropriate modifiers.
/// </summary>
/// <remarks>
/// <para>
/// This registry mirrors the design of <see cref="Parsers.ReasoningParserRegistry"/> but for
/// request modification instead of response parsing.
/// </para>
/// <para>
/// The registry supports:
/// - Registration by provider family (e.g., "opensource", "openai")
/// - Model ID prefix matching for provider detection
/// - Fallback to default modifier if specified
/// </para>
/// </remarks>
public sealed class ReasoningRequestModifierRegistry
{
    private readonly Dictionary<string, IReasoningRequestModifier> _modifiersByProvider = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Prefix, string Provider)> _modelPrefixes = [];
    private IReasoningRequestModifier? _defaultModifier;

    /// <summary>
    /// Gets a shared default registry instance with standard modifiers registered.
    /// </summary>
    public static ReasoningRequestModifierRegistry Default { get; } = CreateDefault();

    /// <summary>
    /// Creates a new empty modifier registry.
    /// </summary>
    public ReasoningRequestModifierRegistry()
    {
    }

    /// <summary>
    /// Creates a default registry with OpenSource modifier for DeepSeek/vLLM.
    /// </summary>
    private static ReasoningRequestModifierRegistry CreateDefault()
    {
        var registry = new ReasoningRequestModifierRegistry();

        // Register OpenSource modifier for DeepSeek, Qwen, vLLM, GPUStack
        var openSourceModifier = new OpenSourceRequestModifier();
        registry.Register(openSourceModifier);
        registry.RegisterModelPrefix("deepseek", OpenSourceRequestModifier.Provider);
        registry.RegisterModelPrefix("qwen", OpenSourceRequestModifier.Provider);
        registry.RegisterModelPrefix("qwq", OpenSourceRequestModifier.Provider);
        registry.RegisterModelPrefix("glm", OpenSourceRequestModifier.Provider);

        // Note: OpenAI and Anthropic don't require explicit reasoning activation
        // - OpenAI o1/o3/o4 models automatically include reasoning
        // - Anthropic Claude automatically includes thinking blocks
        // - Gemini models automatically include thought signatures

        return registry;
    }

    /// <summary>
    /// Registers a modifier for its provider family.
    /// </summary>
    /// <param name="modifier">The modifier to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when modifier is null.</exception>
    public void Register(IReasoningRequestModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        _modifiersByProvider[modifier.ProviderFamily] = modifier;
    }

    /// <summary>
    /// Registers a model ID prefix to provider mapping.
    /// </summary>
    /// <param name="prefix">The model ID prefix (case-insensitive).</param>
    /// <param name="providerFamily">The provider family to map to.</param>
    /// <exception cref="ArgumentException">Thrown when prefix or provider is empty.</exception>
    public void RegisterModelPrefix(string prefix, string providerFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerFamily);

        _modelPrefixes.Add((prefix.ToLowerInvariant(), providerFamily.ToLowerInvariant()));
    }

    /// <summary>
    /// Sets the default modifier to use when no specific modifier matches.
    /// </summary>
    /// <param name="modifier">The default modifier, or null to clear.</param>
    public void SetDefaultModifier(IReasoningRequestModifier? modifier)
    {
        _defaultModifier = modifier;
    }

    /// <summary>
    /// Gets a modifier by provider family.
    /// </summary>
    /// <param name="providerFamily">The provider family (e.g., "opensource", "openai").</param>
    /// <returns>The registered modifier, or null if not found.</returns>
    public IReasoningRequestModifier? GetByProvider(string providerFamily)
    {
        if (string.IsNullOrWhiteSpace(providerFamily))
        {
            return _defaultModifier;
        }

        return _modifiersByProvider.TryGetValue(providerFamily, out var modifier)
            ? modifier
            : _defaultModifier;
    }

    /// <summary>
    /// Gets a modifier for a specific model ID.
    /// </summary>
    /// <param name="modelId">The model ID (e.g., "deepseek-r1", "qwen-72b").</param>
    /// <returns>The appropriate modifier, or null if not found.</returns>
    public IReasoningRequestModifier? GetByModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return _defaultModifier;
        }

        var lowerModelId = modelId.ToLowerInvariant();

        // Find matching prefix
        foreach (var (prefix, provider) in _modelPrefixes)
        {
            if (lowerModelId.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (_modifiersByProvider.TryGetValue(provider, out var modifier))
                {
                    return modifier;
                }
            }
        }

        return _defaultModifier;
    }

    /// <summary>
    /// Tries to get a modifier by provider family.
    /// </summary>
    /// <param name="providerFamily">The provider family.</param>
    /// <param name="modifier">The modifier if found.</param>
    /// <returns>True if a modifier was found; otherwise, false.</returns>
    public bool TryGetByProvider(string providerFamily, [NotNullWhen(true)] out IReasoningRequestModifier? modifier)
    {
        modifier = GetByProvider(providerFamily);
        return modifier is not null;
    }

    /// <summary>
    /// Tries to get a modifier by model ID.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    /// <param name="modifier">The modifier if found.</param>
    /// <returns>True if a modifier was found; otherwise, false.</returns>
    public bool TryGetByModel(string? modelId, [NotNullWhen(true)] out IReasoningRequestModifier? modifier)
    {
        modifier = GetByModel(modelId);
        return modifier is not null;
    }

    /// <summary>
    /// Checks if a model requires explicit reasoning activation.
    /// </summary>
    /// <param name="modelId">The model ID to check.</param>
    /// <returns>True if the model requires explicit activation; otherwise, false.</returns>
    public bool RequiresExplicitActivation(string? modelId)
    {
        return GetByModel(modelId) is not null;
    }

    /// <summary>
    /// Detects the provider family from a model ID.
    /// </summary>
    /// <param name="modelId">The model ID to analyze.</param>
    /// <returns>The detected provider family, or null if unknown.</returns>
    public string? DetectProvider(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        var lowerModelId = modelId.ToLowerInvariant();

        foreach (var (prefix, provider) in _modelPrefixes)
        {
            if (lowerModelId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all registered provider families.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredProviders => _modifiersByProvider.Keys;

    /// <summary>
    /// Gets all registered model prefixes with their provider mappings.
    /// </summary>
    public IReadOnlyList<(string Prefix, string Provider)> RegisteredPrefixes => _modelPrefixes.AsReadOnly();
}
