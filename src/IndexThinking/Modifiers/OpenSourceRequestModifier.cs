using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;

namespace IndexThinking.Modifiers;

/// <summary>
/// Request modifier for open-source reasoning models (DeepSeek, Qwen, vLLM, GPUStack).
/// </summary>
/// <remarks>
/// <para>
/// Open-source reasoning models typically require explicit activation of reasoning output.
/// This modifier adds the appropriate request parameters based on the model and provider.
/// </para>
/// <para>
/// Supported activation methods:
/// - DeepSeek: <c>include_reasoning: true</c>
/// - vLLM/GPUStack: <c>include_reasoning: true</c> or model-specific settings
/// - Qwen (QwQ): <c>enable_thinking: true</c> or <c>include_reasoning: true</c>
/// </para>
/// </remarks>
public sealed class OpenSourceRequestModifier : IReasoningRequestModifier
{
    /// <summary>
    /// Provider family identifier for open-source models.
    /// </summary>
    public const string Provider = "opensource";

    /// <summary>
    /// Default request field name for enabling reasoning.
    /// </summary>
    public const string DefaultReasoningField = "include_reasoning";

    private readonly OpenSourceReasoningRequestSettings _settings;

    /// <summary>
    /// Creates a new modifier with default settings.
    /// </summary>
    public OpenSourceRequestModifier() : this(new OpenSourceReasoningRequestSettings())
    {
    }

    /// <summary>
    /// Creates a new modifier with custom settings.
    /// </summary>
    /// <param name="settings">The reasoning request settings.</param>
    public OpenSourceRequestModifier(OpenSourceReasoningRequestSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public string ProviderFamily => Provider;

    /// <inheritdoc />
    public bool SupportsModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        var lowerModelId = modelId.ToLowerInvariant();

        // Check for known open-source reasoning model prefixes
        return lowerModelId.StartsWith("deepseek", StringComparison.Ordinal) ||
               lowerModelId.StartsWith("qwen", StringComparison.Ordinal) ||
               lowerModelId.StartsWith("qwq", StringComparison.Ordinal) ||
               lowerModelId.StartsWith("glm", StringComparison.Ordinal) ||
               lowerModelId.Contains("r1") ||  // DeepSeek-R1 variants
               lowerModelId.Contains("thinking"); // Models with thinking in name
    }

    /// <inheritdoc />
    public ChatOptions EnableReasoning(ChatOptions? options, string? modelId)
    {
        options ??= new ChatOptions();
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();

        // Determine the appropriate field name based on model
        var fieldName = GetReasoningFieldName(modelId);

        // Add the reasoning activation flag
        options.AdditionalProperties[fieldName] = true;

        // Add model-specific settings if applicable
        ApplyModelSpecificSettings(options, modelId);

        return options;
    }

    /// <summary>
    /// Gets the appropriate request field name for enabling reasoning.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The field name to use in the request.</returns>
    private string GetReasoningFieldName(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return _settings.DefaultRequestField;
        }

        var lowerModelId = modelId.ToLowerInvariant();

        // Check for model-specific overrides
        foreach (var (prefix, fieldName) in _settings.ModelFieldOverrides)
        {
            if (lowerModelId.StartsWith(prefix.ToLowerInvariant(), StringComparison.Ordinal))
            {
                return fieldName;
            }
        }

        return _settings.DefaultRequestField;
    }

    /// <summary>
    /// Applies model-specific settings beyond the basic reasoning flag.
    /// </summary>
    /// <param name="options">The chat options to modify.</param>
    /// <param name="modelId">The model identifier.</param>
    private void ApplyModelSpecificSettings(ChatOptions options, string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId) || options.AdditionalProperties is null)
        {
            return;
        }

        var lowerModelId = modelId.ToLowerInvariant();

        // DeepSeek R1 specific: may need reasoning_format for vLLM
        if (lowerModelId.Contains("deepseek") && _settings.IncludeReasoningFormat)
        {
            options.AdditionalProperties["reasoning_format"] = "parsed";
        }

        // Qwen/QwQ specific: enable_thinking alternative
        if ((lowerModelId.StartsWith("qwen", StringComparison.Ordinal) ||
             lowerModelId.StartsWith("qwq", StringComparison.Ordinal)) &&
            _settings.UseAlternativeQwenField)
        {
            options.AdditionalProperties["enable_thinking"] = true;
        }
    }
}

/// <summary>
/// Settings for open-source reasoning request modification.
/// </summary>
public class OpenSourceReasoningRequestSettings
{
    /// <summary>
    /// Default request field name for enabling reasoning.
    /// Default: "include_reasoning".
    /// </summary>
    public string DefaultRequestField { get; set; } = OpenSourceRequestModifier.DefaultReasoningField;

    /// <summary>
    /// Model prefix to field name overrides.
    /// Key: model ID prefix (case-insensitive), Value: request field name.
    /// </summary>
    public Dictionary<string, string> ModelFieldOverrides { get; set; } = [];

    /// <summary>
    /// Whether to include reasoning_format field for vLLM compatibility.
    /// Default: false.
    /// </summary>
    public bool IncludeReasoningFormat { get; set; }

    /// <summary>
    /// Whether to use enable_thinking as an alternative field for Qwen models.
    /// Default: false.
    /// </summary>
    public bool UseAlternativeQwenField { get; set; }
}
