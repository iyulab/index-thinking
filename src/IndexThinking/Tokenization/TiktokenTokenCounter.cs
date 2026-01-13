using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;

namespace IndexThinking.Tokenization;

/// <summary>
/// Token counter using Microsoft.ML.Tokenizers for OpenAI models.
/// Provides exact token counts matching the official tiktoken library.
/// </summary>
public sealed class TiktokenTokenCounter : ITokenCounter
{
    private readonly TiktokenTokenizer _tokenizer;
    private readonly string _encoding;

    /// <summary>
    /// Overhead tokens added per chat message for role and formatting.
    /// This accounts for special tokens like &lt;|im_start|&gt;, role, &lt;|im_sep|&gt;, etc.
    /// </summary>
    public const int MessageOverhead = 4;

    /// <summary>
    /// Creates a token counter for the specified encoding.
    /// </summary>
    /// <param name="encoding">The tiktoken encoding name (e.g., "o200k_base", "cl100k_base").</param>
    /// <exception cref="ArgumentException">Thrown when the encoding is not recognized.</exception>
    public TiktokenTokenCounter(string encoding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encoding);

        _encoding = encoding;
        _tokenizer = CreateTokenizer(encoding);
    }

    /// <summary>
    /// Creates a token counter for the specified model.
    /// </summary>
    /// <param name="modelId">The model identifier (e.g., "gpt-4o").</param>
    /// <returns>A token counter configured for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when the model is not recognized.</exception>
    public static TiktokenTokenCounter ForModel(string modelId)
    {
        var encoding = ModelEncodingRegistry.GetEncoding(modelId)
            ?? throw new ArgumentException($"Unknown model: {modelId}", nameof(modelId));

        return new TiktokenTokenCounter(encoding);
    }

    /// <inheritdoc />
    public int Count(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return _tokenizer.CountTokens(text);
    }

    /// <inheritdoc />
    public int Count(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var textTokens = 0;

        // Count tokens in all text content
        foreach (var content in message.Contents)
        {
            if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
            {
                textTokens += Count(textContent.Text);
            }
        }

        // Add overhead for message structure
        return textTokens + MessageOverhead;
    }

    /// <inheritdoc />
    public bool SupportsModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return false;

        var encoding = ModelEncodingRegistry.GetEncoding(modelId);
        return encoding == _encoding;
    }

    /// <summary>
    /// Gets the encoding name used by this counter.
    /// </summary>
    public string Encoding => _encoding;

    private static TiktokenTokenizer CreateTokenizer(string encoding)
    {
        // Use the model name that maps to the encoding
        var modelName = encoding switch
        {
            ModelEncodingRegistry.O200kBase => "gpt-4o",
            ModelEncodingRegistry.Cl100kBase => "gpt-4",
            _ => throw new ArgumentException($"Unknown encoding: {encoding}", nameof(encoding))
        };

        return TiktokenTokenizer.CreateForModel(modelName);
    }
}
