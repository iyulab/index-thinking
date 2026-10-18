using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;

namespace IndexThinking.Context;

/// <summary>
/// Default implementation of <see cref="IContextInjector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Injects conversation history by prepending previous turns as messages.
/// This allows the LLM to understand contextual references naturally.
/// </para>
/// <para>
/// Example transformation:
/// </para>
/// <code>
/// Original: ["Do that thing again"]
///
/// With context: [
///   "Save the file to disk",           // Previous user message
///   "I've saved the file to output.csv", // Previous assistant response
///   "Do that thing again"              // Current message
/// ]
/// </code>
/// </remarks>
public sealed class DefaultContextInjector : IContextInjector
{
    private readonly ContextInjectorOptions _options;
    private readonly ITokenCounter? _tokenCounter;

    /// <summary>
    /// Creates a new injector with default options.
    /// </summary>
    public DefaultContextInjector() : this(ContextInjectorOptions.Default) { }

    /// <summary>
    /// Creates a new injector with the specified options.
    /// </summary>
    public DefaultContextInjector(ContextInjectorOptions options)
        : this(options, tokenCounter: null) { }

    /// <summary>
    /// Creates a new injector with the specified options and an optional token counter
    /// for token-aware context injection.
    /// </summary>
    public DefaultContextInjector(ContextInjectorOptions options, ITokenCounter? tokenCounter)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tokenCounter = tokenCounter;
    }

    /// <inheritdoc />
    public IList<ChatMessage> InjectContext(
        IList<ChatMessage> messages,
        ConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        // If no history or injection disabled, return original
        if (!context.HasHistory || !_options.EnableInjection)
        {
            return messages;
        }

        var result = new List<ChatMessage>();

        // Add context turns as messages
        var turnsToInclude = context.RecentTurns
            .TakeLast(_options.MaxTurnsToInject)
            .ToList();

        // Apply token budget if configured and a token counter is available
        if (_options.MaxContextTokens is { } maxTokens && _tokenCounter is not null)
        {
            turnsToInclude = SelectTurnsWithinBudget(turnsToInclude, messages, maxTokens);
        }

        foreach (var turn in turnsToInclude)
        {
            foreach (var message in HistoryMessages(turn))
            {
                result.Add(message);
            }
        }

        // Add current messages at the end
        foreach (var message in messages)
        {
            result.Add(message);
        }

        return result;
    }

    /// <summary>
    /// The messages a past turn contributes to a request: its user message and the text of its response.
    /// </summary>
    /// <remarks>
    /// Tool calls and tool results are left out. A tracked turn can hold a call without its result (when a function-invoking
    /// client sits outside this one, each round's response is the call alone), and a provider rejects a request whose call
    /// has no result. Dropping both keeps the history valid whatever the pipeline order, and keeps the large tool results of
    /// past turns out of the window. Messages are copied, so the tracked turn is never changed.
    /// </remarks>
    private static IEnumerable<ChatMessage> HistoryMessages(ConversationTurn turn)
    {
        if (TextOnly(turn.UserMessage) is { } user)
        {
            yield return user;
        }

        if (turn.AssistantResponse?.Messages is not { } responseMessages)
        {
            yield break;
        }

        foreach (var message in responseMessages)
        {
            if (message.Role != ChatRole.Tool && TextOnly(message) is { } text)
            {
                yield return text;
            }
        }
    }

    private static ChatMessage? TextOnly(ChatMessage message)
    {
        var contents = message.Contents
            .Where(c => c is not FunctionCallContent and not FunctionResultContent)
            .ToList();
        if (contents.Count == 0)
        {
            return null;
        }

        return new ChatMessage(message.Role, contents)
        {
            AuthorName = message.AuthorName,
            MessageId = message.MessageId,
        };
    }

    /// <summary>
    /// Selects turns from most recent to oldest that fit within the token budget.
    /// The budget is calculated as MaxContextTokens minus the tokens used by current messages.
    /// </summary>
    private List<ConversationTurn> SelectTurnsWithinBudget(
        List<ConversationTurn> turns,
        IList<ChatMessage> currentMessages,
        int maxTokens)
    {
        // Reserve tokens for current messages
        var currentTokens = 0;
        foreach (var message in currentMessages)
        {
            currentTokens += _tokenCounter!.CountMessage(message);
        }

        var remainingBudget = maxTokens - currentTokens;
        if (remainingBudget <= 0)
        {
            return [];
        }

        // Walk from most recent turn to oldest, accumulating token cost
        var selected = new List<ConversationTurn>();
        for (var i = turns.Count - 1; i >= 0; i--)
        {
            var turn = turns[i];
            var turnTokens = 0;
            foreach (var msg in HistoryMessages(turn))
            {
                turnTokens += _tokenCounter!.CountMessage(msg);
            }

            if (turnTokens > remainingBudget)
            {
                break;
            }

            selected.Add(turn);
            remainingBudget -= turnTokens;
        }

        // Reverse to restore chronological order
        selected.Reverse();
        return selected;
    }
}

/// <summary>
/// Configuration options for context injection.
/// </summary>
public sealed record ContextInjectorOptions
{
    /// <summary>
    /// Whether to enable context injection.
    /// Default: true.
    /// </summary>
    public bool EnableInjection { get; set; } = true;

    /// <summary>
    /// Maximum number of previous turns to inject.
    /// Default: 5 turns.
    /// </summary>
    /// <remarks>
    /// This limits the amount of history included to prevent
    /// excessive token usage. Consider the LLM's context window
    /// when setting this value.
    /// </remarks>
    public int MaxTurnsToInject { get; set; } = 5;

    /// <summary>
    /// Maximum context tokens for token-aware injection.
    /// When set with a token counter, turns are selected within this budget.
    /// Default: null (no token limit, only MaxTurnsToInject applies).
    /// </summary>
    public int? MaxContextTokens { get; set; }

    /// <summary>
    /// Default options.
    /// </summary>
    public static ContextInjectorOptions Default { get; } = new();
}
