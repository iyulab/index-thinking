using System.Globalization;
using IndexThinking.Abstractions;
using IndexThinking.Tokenization;
using Microsoft.Extensions.AI;

namespace IndexThinking.Reduction;

/// <summary>
/// Fits one model call's messages into a token budget without breaking tool rounds: a tool result over
/// <see cref="ContextBudgetOptions.MaxToolResultChars"/> keeps its head and tail, and when the messages are still over
/// <see cref="ContextBudgetOptions.MaxInputTokens"/> the oldest messages are dropped — a tool round (the assistant message
/// that calls tools and the results that answer it) always as a whole, so no request carries a call without its result or a
/// result without its call (which providers reject). Leading system messages, the last user message and the newest message
/// or tool round are kept; each dropped run is replaced by one note where it was.
/// </summary>
/// <remarks>
/// <para>Use it in front of any <see cref="IChatClient"/> with M.E.AI's <c>ChatClientBuilder.UseChatReducer(reducer)</c>. To
/// reduce every round of a tool loop — where the history grows — place it inside <c>UseFunctionInvocation()</c>
/// (registered after it), so it wraps each model call the loop makes rather than only the first.</para>
/// <para>The reducer only shapes the request; the caller's history is untouched. It keeps no state, so the same messages
/// always reduce to the same request.</para>
/// </remarks>
public sealed class ContextBudgetChatReducer : IChatReducer
{
    private readonly ContextBudgetOptions _options;
    private readonly ITokenCounter _counter;

    /// <summary>Creates the reducer.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A budget or limit is not positive.</exception>
    public ContextBudgetChatReducer(ContextBudgetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxInputTokens, nameof(options));
        if (options.MaxToolResultChars is { } max)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max, nameof(options));
        }

        _options = options;
        _counter = options.TokenCounter ?? new ApproximateTokenCounter();
    }

    /// <inheritdoc />
    public Task<IEnumerable<ChatMessage>> ReduceAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return Task.FromResult<IEnumerable<ChatMessage>>(Reduce([.. messages]));
    }

    internal IReadOnlyList<ChatMessage> Reduce(IReadOnlyList<ChatMessage> messages)
    {
        var shortened = _options.MaxToolResultChars is { } maxChars ? ShortenToolResults(messages, maxChars) : messages;

        var lead = 0;
        while (lead < shortened.Count && shortened[lead].Role == ChatRole.System)
        {
            lead++;
        }

        var units = Units(shortened, lead);
        var tokens = shortened.Sum(Count);
        if (tokens <= _options.MaxInputTokens || units.Count <= 1)
        {
            return shortened;
        }

        // Drop units from the oldest until the request fits — never the newest unit, and never the last user message (the
        // request the model is working on); a dropped run is replaced by one note where it was.
        var lastUser = units.FindLastIndex(u => shortened[u.Start].Role == ChatRole.User);
        var dropped = new bool[units.Count];
        var noteTokens = Count(OmissionNote(1));
        for (var u = 0; u < units.Count - 1 && tokens + (2 * noteTokens) > _options.MaxInputTokens; u++)
        {
            if (u == lastUser)
            {
                continue;
            }

            dropped[u] = true;
            for (var i = units[u].Start; i < units[u].End; i++)
            {
                tokens -= Count(shortened[i]);
            }
        }

        var result = new List<ChatMessage>(shortened.Count);
        result.AddRange(shortened.Take(lead));
        var run = 0;
        for (var u = 0; u < units.Count; u++)
        {
            var (start, end) = units[u];
            if (dropped[u])
            {
                run += end - start;
                continue;
            }

            if (run > 0)
            {
                result.Add(OmissionNote(run));
                run = 0;
            }

            for (var i = start; i < end; i++)
            {
                result.Add(shortened[i]);
            }
        }

        return result;
    }

    private int Count(ChatMessage message) => ChatMessageContents.Count(message, _counter.Count, ApproximateTokenCounter.MessageOverhead);

    /// <summary>
    /// The units after the leading system messages, oldest first: a user message; an assistant message that calls tools
    /// together with the tool messages that follow it (one tool round — never split); any other message on its own.
    /// </summary>
    private static List<(int Start, int End)> Units(IReadOnlyList<ChatMessage> messages, int lead)
    {
        var units = new List<(int, int)>();
        var i = lead;
        while (i < messages.Count)
        {
            var start = i++;
            if (messages[start].Role == ChatRole.Assistant && HasToolCalls(messages[start]))
            {
                while (i < messages.Count && messages[i].Role == ChatRole.Tool)
                {
                    i++;
                }
            }

            units.Add((start, i));
        }

        return units;
    }

    private static bool HasToolCalls(ChatMessage message) => message.Contents.Any(c => c is FunctionCallContent);

    /// <summary>
    /// Stands where dropped messages were. A user message, not a system one: a request whose first message after the
    /// system messages is the assistant's is rejected by some providers, and a system message mid-conversation by others.
    /// </summary>
    private static ChatMessage OmissionNote(int droppedMessages) => new(ChatRole.User, string.Create(CultureInfo.InvariantCulture,
        $"[{droppedMessages} earlier messages omitted to fit the context window]"));

    private static IReadOnlyList<ChatMessage> ShortenToolResults(IReadOnlyList<ChatMessage> messages, int maxChars)
    {
        List<ChatMessage>? result = null;
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (message.Role == ChatRole.Tool && message.Contents.Any(c => c is FunctionResultContent r && ResultText(r).Length > maxChars))
            {
                result ??= [.. messages.Take(i)];
                result.Add(new ChatMessage(ChatRole.Tool, [.. message.Contents.Select(c =>
                    c is FunctionResultContent r && ResultText(r) is { } text && text.Length > maxChars
                        ? new FunctionResultContent(r.CallId, HeadAndTail(text, maxChars))
                        : c)]));
                continue;
            }

            result?.Add(message);
        }

        return result ?? messages;
    }

    private static string ResultText(FunctionResultContent result) => result.Result switch
    {
        null => string.Empty,
        string s => s,
        _ => result.Result.ToString() ?? string.Empty,
    };

    /// <summary>The first and last parts of <paramref name="text"/> within <paramref name="maxChars"/>, cut at line ends where possible.</summary>
    internal static string HeadAndTail(string text, int maxChars)
    {
        var marker = string.Create(CultureInfo.InvariantCulture, $"\n[... {text.Length - maxChars:N0} of {text.Length:N0} characters omitted ...]\n");
        var keep = Math.Max(0, maxChars - marker.Length);
        var headLength = keep * 2 / 3;
        var tailLength = keep - headLength;
        var head = text[..headLength];
        var tail = text[^tailLength..];
        var headCut = head.LastIndexOf('\n');
        if (headCut > headLength / 2)
        {
            head = head[..headCut];
        }

        var tailCut = tail.IndexOf('\n');
        if (tailCut >= 0 && tailCut < tailLength / 2)
        {
            tail = tail[(tailCut + 1)..];
        }

        return string.Concat(head, marker, tail);
    }
}
