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
            // Add user message from history
            result.Add(turn.UserMessage);

            // Add assistant response if available
            if (turn.AssistantResponse?.Messages != null)
            {
                foreach (var assistantMessage in turn.AssistantResponse.Messages)
                {
                    result.Add(assistantMessage);
                }
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
            currentTokens += _tokenCounter!.Count(message);
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
            var turnTokens = _tokenCounter!.Count(turn.UserMessage);

            if (turn.AssistantResponse?.Messages != null)
            {
                foreach (var msg in turn.AssistantResponse.Messages)
                {
                    turnTokens += _tokenCounter.Count(msg);
                }
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
