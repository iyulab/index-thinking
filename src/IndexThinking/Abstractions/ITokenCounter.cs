namespace IndexThinking.Abstractions;

/// <summary>
/// Type-neutral token counter: text and model-level members only, no framework message types.
/// Consumers with their own message shapes can implement or consume this interface without a
/// Microsoft.Extensions.AI type coupling; M.E.AI consumers use <see cref="IChatMessageTokenCounter"/>.
/// </summary>
public interface ITokenCounter
{
    /// <summary>Counts tokens in a text string.</summary>
    int Count(string text);

    /// <summary>Counts tokens across a sequence of text strings.</summary>
    int Count(IEnumerable<string> texts);

    /// <summary>Returns true if this counter supports the given model.</summary>
    bool SupportsModel(string modelId);

    /// <summary>Returns true if this counter produces approximate results for the given model.</summary>
    bool IsApproximate(string modelId) => false;
}
