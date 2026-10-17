using System.Globalization;
using System.Text.Json;
using IndexThinking.Abstractions;
using IndexThinking.Tokenization;
using Microsoft.Extensions.AI;

namespace IndexThinking.Reduction;

/// <summary>
/// Keeps the original of a tool result a <see cref="ToolResultMasker"/> masks, so the model can read it back by the id the
/// placeholder names instead of repeating the call (IronHive.Agent's <c>CompactedResultTool</c> is one).
/// </summary>
public interface IToolResultRetention
{
    /// <summary>The tool that reads a kept result back; the placeholder tells the model to call it.</summary>
    string ToolName { get; }

    /// <summary>Keeps <paramref name="text"/> as the original of <paramref name="result"/> and returns the id to read it by.</summary>
    string Retain(FunctionResultContent result, string text);
}

/// <summary>
/// Replaces old tool results with a short placeholder that names the call (tool and arguments), how large the result was,
/// and how to get it again — the call kept, so the model still knows what it read. Results in the newest user turns are
/// kept, and with a token budget also only as many recent results as fit it (measured from the newest back; the newest
/// round always stays). The placeholder depends only on the call and the result, so a result masks to the same text on
/// every request.
/// </summary>
public sealed class ToolResultMasker
{
    /// <summary>Longest argument text a placeholder repeats; longer arguments are cut with an ellipsis.</summary>
    internal const int MaxArgumentsLength = 200;

    private static readonly JsonSerializerOptions ArgumentsJson = new(AIJsonUtilities.DefaultOptions) { WriteIndented = false };

    private readonly ITokenCounter _counter;
    private readonly int _protectedTurns;
    private readonly int _minimumResultLength;
    private readonly int? _protectedTokens;
    private readonly int _minimumClearTokens;

    /// <summary>Creates a masker.</summary>
    /// <param name="protectedTurns">Newest user turns whose results stay (a turn runs from a user message to the next). Default 3.</param>
    /// <param name="minimumResultLength">Results shorter than this many characters stay. Default 200.</param>
    /// <param name="protectedTokens">
    /// Token budget for the newest results, counted from the newest back across turns: results past it are masked even in a
    /// protected turn — one user message followed by many tool rounds keeps only what fits. <c>null</c> (default): off.
    /// </param>
    /// <param name="tokenCounter">
    /// Counts results against <paramref name="protectedTokens"/> and <paramref name="minimumClearTokens"/>. Default:
    /// <see cref="ApproximateTokenCounter"/>.
    /// </param>
    /// <param name="minimumClearTokens">
    /// Mask only when the results that would be masked add up to at least this many tokens; otherwise leave the history as
    /// it is. Masking changes the prompt from the first masked result on, so a provider's prompt cache misses from there:
    /// a new turn moves the protection boundary by one turn, and masking at every move makes every turn miss. With a
    /// threshold the results wait past the boundary until masking them is worth the miss (the same trade-off as
    /// Anthropic's context-editing <c>clear_at_least</c>). Results already masked do not count. 0 (default): mask at
    /// every move.
    /// </param>
    public ToolResultMasker(
        int protectedTurns = 3, int minimumResultLength = 200, int? protectedTokens = null, ITokenCounter? tokenCounter = null,
        int minimumClearTokens = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(protectedTurns);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumResultLength);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumClearTokens);
        if (protectedTokens is { } tokens)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tokens, nameof(protectedTokens));
        }

        _protectedTurns = protectedTurns;
        _minimumResultLength = minimumResultLength;
        _protectedTokens = protectedTokens;
        _minimumClearTokens = minimumClearTokens;
        _counter = tokenCounter ?? new ApproximateTokenCounter();
    }

    /// <summary>
    /// <paramref name="messages"/> with old results masked, or <paramref name="messages"/> itself when nothing is. With
    /// <paramref name="retention"/>, each masked result's text is kept there and the placeholder names its id and tool.
    /// </summary>
    public IReadOnlyList<ChatMessage> Mask(IReadOnlyList<ChatMessage> messages, IToolResultRetention? retention = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
        {
            return messages;
        }

        var turnStart = ProtectedStart(messages);
        var overBudget = _protectedTokens is { } budget ? ResultsOverBudget(messages, budget) : null;
        if (turnStart <= 0 && (overBudget is null || overBudget.Count == 0))
        {
            return messages;
        }

        bool ShouldMask(int index, FunctionResultContent r) => index < turnStart || (overBudget is not null && overBudget.Contains(r));

        if (_minimumClearTokens > 0 && MaskableTokens(messages, ShouldMask) < _minimumClearTokens)
        {
            return messages;
        }

        var calls = CallsById(messages);
        var result = new List<ChatMessage>(messages.Count);
        var anyMasked = false;
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (message.Role != ChatRole.Tool)
            {
                result.Add(message);
                continue;
            }

            var index = i;
            var masked = MaskToolMessage(message, calls, r => ShouldMask(index, r), retention);
            result.Add(masked);
            anyMasked |= !ReferenceEquals(masked, message);
        }

        return anyMasked ? result : messages;
    }

    // The tokens a Mask call would take out: the results it would replace, placeholders it made on an earlier request excluded.
    private int MaskableTokens(IReadOnlyList<ChatMessage> messages, Func<int, FunctionResultContent, bool> shouldMask)
    {
        var total = 0;
        for (var i = 0; i < messages.Count; i++)
        {
            if (messages[i].Role != ChatRole.Tool)
            {
                continue;
            }

            foreach (var result in messages[i].Contents.OfType<FunctionResultContent>())
            {
                var text = ToolResultTexts.Of(result.Result);
                if (shouldMask(i, result) && IsMaskable(text))
                {
                    total += _counter.Count(text);
                }
            }
        }

        return total;
    }

    // A placeholder is never masked again: a loop stores the prepared history, so the next request sees placeholders it made
    // itself, and one with long arguments is longer than the minimum length.
    private bool IsMaskable(string text) =>
        text.Length >= _minimumResultLength && !text.StartsWith(PlaceholderPrefix, StringComparison.Ordinal);

    private const string PlaceholderPrefix = "[Masked: ";

    private static Dictionary<string, FunctionCallContent> CallsById(IReadOnlyList<ChatMessage> messages)
    {
        var map = new Dictionary<string, FunctionCallContent>(StringComparer.Ordinal);
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.Assistant)
            {
                continue;
            }

            foreach (var call in message.Contents.OfType<FunctionCallContent>())
            {
                if (call.CallId is not null)
                {
                    map[call.CallId] = call;
                }
            }
        }

        return map;
    }

    private int ProtectedStart(IReadOnlyList<ChatMessage> messages)
    {
        if (_protectedTurns <= 0)
        {
            return messages.Count;
        }

        var turns = 0;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == ChatRole.User && ++turns >= _protectedTurns)
            {
                return i;
            }
        }

        return 0;
    }

    private HashSet<FunctionResultContent> ResultsOverBudget(IReadOnlyList<ChatMessage> messages, int budget)
    {
        var over = new HashSet<FunctionResultContent>(ReferenceEqualityComparer.Instance);
        var newestRoundStart = 0;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == ChatRole.Assistant && messages[i].Contents.OfType<FunctionCallContent>().Any())
            {
                newestRoundStart = i;
                break;
            }
        }

        var used = 0;
        var exceeded = false;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role != ChatRole.Tool)
            {
                continue;
            }

            var contents = messages[i].Contents;
            for (var c = contents.Count - 1; c >= 0; c--)
            {
                if (contents[c] is not FunctionResultContent result)
                {
                    continue;
                }

                if (exceeded)
                {
                    over.Add(result);
                    continue;
                }

                used += _counter.Count(ToolResultTexts.Of(result.Result));
                if (used > budget && i < newestRoundStart)
                {
                    exceeded = true;
                    over.Add(result);
                }
            }
        }

        return over;
    }

    private ChatMessage MaskToolMessage(
        ChatMessage message, Dictionary<string, FunctionCallContent> calls, Func<FunctionResultContent, bool> shouldMask,
        IToolResultRetention? retention)
    {
        List<AIContent>? masked = null;
        for (var i = 0; i < message.Contents.Count; i++)
        {
            var content = message.Contents[i];
            if (content is FunctionResultContent result && shouldMask(result))
            {
                var text = ToolResultTexts.Of(result.Result);
                if (IsMaskable(text))
                {
                    masked ??= [.. message.Contents.Take(i)];
                    var id = retention?.Retain(result, text);
                    masked.Add(new FunctionResultContent(result.CallId, Placeholder(result, text, calls, retention, id)));
                    continue;
                }
            }

            masked?.Add(content);
        }

        return masked is null ? message : new ChatMessage(ChatRole.Tool, masked);
    }

    private static string Placeholder(
        FunctionResultContent result, string text, Dictionary<string, FunctionCallContent> calls, IToolResultRetention? retention, string? id)
    {
        var call = result.CallId is not null && calls.TryGetValue(result.CallId, out var found) ? found : null;
        var toolName = call?.Name ?? "tool";
        var arguments = FormatArguments(call);
        var lineCount = text.Count(c => c == '\n') + 1;
        var invocation = arguments.Length > 0 ? $"{toolName} {arguments}" : toolName;
        var again = retention is not null && id is not null
            ? $"call {retention.ToolName} with id \"{id}\" to read it again (do not repeat the call)"
            : call is not null
                ? $"call {toolName} again with the same arguments if you need it"
                : "repeat the call that produced it if you need it";
        return string.Create(CultureInfo.InvariantCulture,
            $"{PlaceholderPrefix}{invocation} result, {text.Length:N0} chars, ~{lineCount} lines. The content is no longer visible here; {again}.]");
    }

    private static string FormatArguments(FunctionCallContent? call)
    {
        if (call?.Arguments is not { Count: > 0 } arguments)
        {
            return string.Empty;
        }

        string text;
        try
        {
            text = JsonSerializer.Serialize(arguments, ArgumentsJson.GetTypeInfo(typeof(IDictionary<string, object?>)));
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
        {
            return string.Empty;
        }

        return text.Length <= MaxArgumentsLength ? text : string.Concat(text.AsSpan(0, MaxArgumentsLength), "…");
    }
}
