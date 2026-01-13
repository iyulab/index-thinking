namespace IndexThinking.Tokenization;

/// <summary>
/// Maps model identifiers to their corresponding tiktoken encodings.
/// </summary>
public static class ModelEncodingRegistry
{
    /// <summary>
    /// The o200k_base encoding used by GPT-4o and O-series models.
    /// </summary>
    public const string O200kBase = "o200k_base";

    /// <summary>
    /// The cl100k_base encoding used by GPT-4 and GPT-3.5 models.
    /// </summary>
    public const string Cl100kBase = "cl100k_base";

    // o200k_base models (GPT-4o, O-series reasoning models)
    private static readonly HashSet<string> O200kModels = new(StringComparer.OrdinalIgnoreCase)
    {
        // GPT-4o family
        "gpt-4o",
        "gpt-4o-mini",
        "gpt-4o-2024-05-13",
        "gpt-4o-2024-08-06",
        "gpt-4o-2024-11-20",
        "chatgpt-4o-latest",

        // O-series reasoning models
        "o1",
        "o1-mini",
        "o1-preview",
        "o1-2024-12-17",
        "o3",
        "o3-mini",
        "o4-mini"
    };

    // cl100k_base models (GPT-4, GPT-3.5)
    private static readonly HashSet<string> Cl100kModels = new(StringComparer.OrdinalIgnoreCase)
    {
        // GPT-4 family
        "gpt-4",
        "gpt-4-turbo",
        "gpt-4-turbo-preview",
        "gpt-4-32k",
        "gpt-4-0125-preview",
        "gpt-4-1106-preview",

        // GPT-3.5 family
        "gpt-3.5-turbo",
        "gpt-3.5-turbo-16k",
        "gpt-3.5-turbo-0125",
        "gpt-3.5-turbo-1106",

        // Azure deployment names
        "gpt-35-turbo",
        "gpt-35-turbo-16k"
    };

    /// <summary>
    /// Gets the tiktoken encoding name for the specified model.
    /// </summary>
    /// <param name="modelId">The model identifier (e.g., "gpt-4o", "gpt-4").</param>
    /// <returns>The encoding name if the model is recognized; otherwise, null.</returns>
    public static string? GetEncoding(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return null;

        // Normalize: remove version suffixes for prefix matching
        var normalizedId = NormalizeModelId(modelId);

        if (O200kModels.Contains(normalizedId) || IsO200kPrefix(normalizedId))
            return O200kBase;

        if (Cl100kModels.Contains(normalizedId) || IsCl100kPrefix(normalizedId))
            return Cl100kBase;

        return null;
    }

    /// <summary>
    /// Determines if the specified model is an OpenAI model.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>True if the model is recognized as an OpenAI model; otherwise, false.</returns>
    public static bool IsOpenAIModel(string modelId)
    {
        return GetEncoding(modelId) is not null;
    }

    /// <summary>
    /// Gets all known model IDs that use a specific encoding.
    /// </summary>
    /// <param name="encoding">The encoding name (e.g., "o200k_base").</param>
    /// <returns>A collection of model IDs using that encoding.</returns>
    public static IReadOnlyCollection<string> GetModelsForEncoding(string encoding)
    {
        return encoding switch
        {
            O200kBase => O200kModels,
            Cl100kBase => Cl100kModels,
            _ => Array.Empty<string>()
        };
    }

    private static string NormalizeModelId(string modelId)
    {
        // Handle common patterns like "gpt-4o-2024-05-13" -> check exact match first
        return modelId.Trim();
    }

    private static bool IsO200kPrefix(string modelId)
    {
        // Match models with prefixes like "gpt-4o-*", "o1-*", "o3-*", "o4-*"
        return modelId.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCl100kPrefix(string modelId)
    {
        // Match models with prefixes like "gpt-4-*" (but not "gpt-4o"), "gpt-3.5-*"
        if (modelId.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase))
            return false; // This is o200k

        return modelId.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("gpt-3.5", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("gpt-35", StringComparison.OrdinalIgnoreCase);
    }
}
