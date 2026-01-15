using System.Runtime.CompilerServices;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Context;
using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Client;

/// <summary>
/// A delegating chat client that orchestrates thinking turns with complexity estimation,
/// budget tracking, continuation handling, and conversation context management.
/// </summary>
/// <remarks>
/// This client wraps any <see cref="IChatClient"/> and provides:
/// <list type="bullet">
///   <item>Automatic truncation detection and continuation</item>
///   <item>Task complexity estimation and budget management</item>
///   <item>Reasoning content extraction and parsing</item>
///   <item>Turn state management</item>
///   <item>Conversation context tracking and injection (v0.9.0)</item>
/// </list>
/// </remarks>
public class ThinkingChatClient : DelegatingChatClient
{
    private readonly IThinkingTurnManager _turnManager;
    private readonly ThinkingChatClientOptions _options;
    private readonly IContextTracker? _contextTracker;
    private readonly IContextInjector? _contextInjector;

    /// <summary>
    /// Metadata key for storing <see cref="ThinkingContent"/> in the response.
    /// </summary>
    public const string ThinkingContentKey = "IndexThinking.ThinkingContent";

    /// <summary>
    /// Metadata key for storing <see cref="TurnMetrics"/> in the response.
    /// </summary>
    public const string TurnMetricsKey = "IndexThinking.TurnMetrics";

    /// <summary>
    /// Metadata key for storing <see cref="TurnResult"/> in the response.
    /// </summary>
    public const string TurnResultKey = "IndexThinking.TurnResult";

    /// <summary>
    /// Creates a new <see cref="ThinkingChatClient"/>.
    /// </summary>
    /// <param name="innerClient">The inner client to delegate to.</param>
    /// <param name="turnManager">The turn manager for orchestrating thinking turns.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="contextTracker">Optional context tracker for conversation management.</param>
    /// <param name="contextInjector">Optional context injector for message enrichment.</param>
    public ThinkingChatClient(
        IChatClient innerClient,
        IThinkingTurnManager turnManager,
        ThinkingChatClientOptions? options = null,
        IContextTracker? contextTracker = null,
        IContextInjector? contextInjector = null)
        : base(innerClient)
    {
        _turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
        _options = options ?? new ThinkingChatClientOptions();
        _contextTracker = contextTracker;
        _contextInjector = contextInjector;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var sessionId = GetSessionId(options);

        // Inject conversation context if enabled
        var enrichedMessages = InjectConversationContext(sessionId, messageList);

        // Create thinking context from messages and options
        var context = CreateContext(enrichedMessages, options, cancellationToken, sessionId);

        // Process through turn manager
        var turnResult = await _turnManager.ProcessTurnAsync(
            context,
            async (msgs, ct) => await base.GetResponseAsync(msgs, options, ct));

        // Track conversation if enabled
        TrackConversation(sessionId, messageList, turnResult.Response);

        // Enrich response with thinking metadata
        return EnrichResponse(turnResult);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For streaming, we need to handle this differently
        // Option 1: Buffer and process (loses streaming benefits)
        // Option 2: Pass through with post-processing (current approach)
        //
        // Currently, streaming bypasses thinking orchestration because:
        // - Continuation detection requires complete response
        // - Budget tracking needs token counts from complete response
        //
        // Future enhancement: implement streaming-aware continuation

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Creates a <see cref="ThinkingContext"/> from the request.
    /// </summary>
    private ThinkingContext CreateContext(
        IList<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken,
        string sessionId)
    {
        var context = ThinkingContext.Create(sessionId, messages)
            .WithBudget(_options.DefaultBudget)
            .WithCancellation(cancellationToken);

        // Apply continuation config
        context = context with { Continuation = _options.DefaultContinuation };

        // Apply model ID if available
        if (options?.ModelId is not null)
        {
            context = context.WithModel(options.ModelId);
        }

        // Apply complexity if specified in options
        if (options?.AdditionalProperties?.TryGetValue("IndexThinking.Complexity", out var complexityObj) == true
            && complexityObj is TaskComplexity complexity)
        {
            context = context.WithComplexity(complexity);
        }

        return context;
    }

    /// <summary>
    /// Injects conversation context into the messages.
    /// </summary>
    private IList<ChatMessage> InjectConversationContext(string sessionId, IList<ChatMessage> messages)
    {
        if (!_options.EnableContextInjection || _contextTracker is null || _contextInjector is null)
        {
            return messages;
        }

        var conversationContext = _contextTracker.GetContext(sessionId);
        if (!conversationContext.HasHistory)
        {
            return messages;
        }

        return _contextInjector.InjectContext(messages, conversationContext);
    }

    /// <summary>
    /// Tracks the conversation turn.
    /// </summary>
    private void TrackConversation(string sessionId, IList<ChatMessage> originalMessages, ChatResponse response)
    {
        if (!_options.EnableContextTracking || _contextTracker is null)
        {
            return;
        }

        // Track the last user message with the response
        var lastUserMessage = originalMessages.LastOrDefault(m => m.Role == ChatRole.User);
        if (lastUserMessage is not null)
        {
            _contextTracker.Track(sessionId, lastUserMessage, response);
        }
    }

    /// <summary>
    /// Gets or generates a session ID from the options.
    /// </summary>
    private string GetSessionId(ChatOptions? options)
    {
        if (options?.AdditionalProperties?.TryGetValue(_options.SessionIdKey, out var sessionIdObj) == true
            && sessionIdObj is string sessionId
            && !string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId;
        }

        return _options.SessionIdFactory();
    }

    /// <summary>
    /// Enriches the response with thinking metadata.
    /// </summary>
    private ChatResponse EnrichResponse(TurnResult turnResult)
    {
        var response = turnResult.Response;

        // Add metadata if enabled
        response.AdditionalProperties ??= new AdditionalPropertiesDictionary();

        if (_options.IncludeThinkingInMetadata && turnResult.ThinkingContent is not null)
        {
            response.AdditionalProperties[ThinkingContentKey] = turnResult.ThinkingContent;
        }

        if (_options.IncludeMetricsInMetadata)
        {
            response.AdditionalProperties[TurnMetricsKey] = turnResult.Metrics;
        }

        // Always include full result for advanced consumers
        response.AdditionalProperties[TurnResultKey] = turnResult;

        return response;
    }
}
