using System.Diagnostics.CodeAnalysis;
using IndexThinking.Abstractions;

namespace IndexThinking.Parsers;

/// <summary>
/// Registry for reasoning parsers, mapping providers and model IDs to appropriate parsers.
/// </summary>
/// <remarks>
/// The registry supports:
/// - Registration by provider family (e.g., "openai", "anthropic")
/// - Model ID prefix matching for provider detection
/// - Fallback to default parser if specified
/// </remarks>
public sealed class ReasoningParserRegistry
{
    private readonly Dictionary<string, IReasoningParser> _parsersByProvider = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Prefix, string Provider)> _modelPrefixes = new();
    private IReasoningParser? _defaultParser;

    /// <summary>
    /// Gets a shared default registry instance with standard parsers registered.
    /// </summary>
    public static ReasoningParserRegistry Default { get; } = CreateDefault();

    /// <summary>
    /// Creates a new empty parser registry.
    /// </summary>
    public ReasoningParserRegistry()
    {
    }

    /// <summary>
    /// Creates a default registry with OpenAI and Anthropic parsers.
    /// </summary>
    private static ReasoningParserRegistry CreateDefault()
    {
        var registry = new ReasoningParserRegistry();

        // Register OpenAI parser
        var openaiParser = new OpenAIReasoningParser();
        registry.Register(openaiParser);
        registry.RegisterModelPrefix("gpt-", OpenAIReasoningParser.Provider);
        registry.RegisterModelPrefix("o1", OpenAIReasoningParser.Provider);
        registry.RegisterModelPrefix("o3", OpenAIReasoningParser.Provider);
        registry.RegisterModelPrefix("o4", OpenAIReasoningParser.Provider);

        // Register Anthropic parser
        var anthropicParser = new AnthropicReasoningParser();
        registry.Register(anthropicParser);
        registry.RegisterModelPrefix("claude", AnthropicReasoningParser.Provider);

        // Register Gemini parser
        var geminiParser = new GeminiReasoningParser();
        registry.Register(geminiParser);
        registry.RegisterModelPrefix("gemini", GeminiReasoningParser.Provider);
        registry.RegisterModelPrefix("models/gemini", GeminiReasoningParser.Provider);

        // Register OpenSource parser (DeepSeek, Qwen, vLLM, etc.)
        var openSourceParser = new OpenSourceReasoningParser();
        registry.Register(openSourceParser);
        registry.RegisterModelPrefix("deepseek", OpenSourceReasoningParser.Provider);
        registry.RegisterModelPrefix("qwen", OpenSourceReasoningParser.Provider);
        registry.RegisterModelPrefix("qwq", OpenSourceReasoningParser.Provider);
        registry.RegisterModelPrefix("glm", OpenSourceReasoningParser.Provider);

        return registry;
    }

    /// <summary>
    /// Registers a parser for its provider family.
    /// </summary>
    /// <param name="parser">The parser to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when parser is null.</exception>
    public void Register(IReasoningParser parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        _parsersByProvider[parser.ProviderFamily] = parser;
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
    /// Sets the default parser to use when no specific parser matches.
    /// </summary>
    /// <param name="parser">The default parser, or null to clear.</param>
    public void SetDefaultParser(IReasoningParser? parser)
    {
        _defaultParser = parser;
    }

    /// <summary>
    /// Gets a parser by provider family.
    /// </summary>
    /// <param name="providerFamily">The provider family (e.g., "openai", "anthropic").</param>
    /// <returns>The registered parser, or null if not found.</returns>
    public IReasoningParser? GetByProvider(string providerFamily)
    {
        if (string.IsNullOrWhiteSpace(providerFamily))
        {
            return _defaultParser;
        }

        return _parsersByProvider.TryGetValue(providerFamily, out var parser)
            ? parser
            : _defaultParser;
    }

    /// <summary>
    /// Gets a parser for a specific model ID.
    /// </summary>
    /// <param name="modelId">The model ID (e.g., "gpt-4o", "claude-3-opus").</param>
    /// <returns>The appropriate parser, or null if not found.</returns>
    public IReasoningParser? GetByModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return _defaultParser;
        }

        var lowerModelId = modelId.ToLowerInvariant();

        // Find matching prefix
        foreach (var (prefix, provider) in _modelPrefixes)
        {
            if (lowerModelId.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (_parsersByProvider.TryGetValue(provider, out var parser))
                {
                    return parser;
                }
            }
        }

        return _defaultParser;
    }

    /// <summary>
    /// Tries to get a parser by provider family.
    /// </summary>
    /// <param name="providerFamily">The provider family.</param>
    /// <param name="parser">The parser if found.</param>
    /// <returns>True if a parser was found; otherwise, false.</returns>
    public bool TryGetByProvider(string providerFamily, [NotNullWhen(true)] out IReasoningParser? parser)
    {
        parser = GetByProvider(providerFamily);
        return parser is not null;
    }

    /// <summary>
    /// Tries to get a parser by model ID.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    /// <param name="parser">The parser if found.</param>
    /// <returns>True if a parser was found; otherwise, false.</returns>
    public bool TryGetByModel(string? modelId, [NotNullWhen(true)] out IReasoningParser? parser)
    {
        parser = GetByModel(modelId);
        return parser is not null;
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
    public IReadOnlyCollection<string> RegisteredProviders => _parsersByProvider.Keys;

    /// <summary>
    /// Gets all registered model prefixes with their provider mappings.
    /// </summary>
    public IReadOnlyList<(string Prefix, string Provider)> RegisteredPrefixes => _modelPrefixes.AsReadOnly();
}
