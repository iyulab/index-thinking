using System.Diagnostics;
using System.Runtime.CompilerServices;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Context;
using IndexThinking.Core;
using IndexThinking.Diagnostics;
using IndexThinking.Modifiers;
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
///   <item>Explicit reasoning activation for providers that require it (v0.12.0)</item>
/// </list>
/// </remarks>
public class ThinkingChatClient : DelegatingChatClient
{
    private readonly IThinkingTurnManager _turnManager;
    private readonly ThinkingChatClientOptions _options;
    private readonly IContextTracker? _contextTracker;
    private readonly IContextInjector? _contextInjector;
    private readonly IndexThinkingMeter? _meter;
    private readonly ReasoningRequestModifierRegistry? _modifierRegistry;

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
    /// <param name="meter">Optional meter for IndexThinking-specific metrics.</param>
    /// <param name="modifierRegistry">Optional registry for reasoning request modifiers.</param>
    public ThinkingChatClient(
        IChatClient innerClient,
        IThinkingTurnManager turnManager,
        ThinkingChatClientOptions? options = null,
        IContextTracker? contextTracker = null,
        IContextInjector? contextInjector = null,
        IndexThinkingMeter? meter = null,
        ReasoningRequestModifierRegistry? modifierRegistry = null)
        : base(innerClient)
    {
        _turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
        _options = options ?? new ThinkingChatClientOptions();
        _contextTracker = contextTracker;
        _contextInjector = contextInjector;
        _meter = meter;
        _modifierRegistry = modifierRegistry ?? (_options.EnableReasoning ? ReasoningRequestModifierRegistry.Default : null);
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

        // Enable reasoning in request if configured
        var modifiedOptions = EnableReasoningIfRequired(options);

        // Inject conversation context if enabled
        var enrichedMessages = InjectConversationContext(sessionId, messageList);

        // Create thinking context from messages and options
        var context = CreateContext(enrichedMessages, modifiedOptions, cancellationToken, sessionId);

        // Process through turn manager
        var turnResult = await _turnManager.ProcessTurnAsync(
            context,
            async (msgs, ct) => await base.GetResponseAsync(msgs, modifiedOptions, ct));

        // Track conversation if enabled
        TrackConversation(sessionId, messageList, turnResult.Response);

        // Add IndexThinking-specific telemetry tags to current Activity
        AddTelemetryTags(turnResult);

        // Record metrics if meter is configured
        _meter?.RecordTurn(turnResult, sessionId);

        // Enrich response with thinking metadata
        return EnrichResponse(turnResult);
    }

    /// <summary>
    /// Streams responses while collecting updates for post-stream thinking orchestration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses the "Collect-and-Yield" pattern: each streaming chunk is yielded to the caller
    /// immediately while simultaneously buffered internally. After the stream completes,
    /// the buffered updates are aggregated into a <see cref="ChatResponse"/> and processed
    /// through the thinking orchestration pipeline (continuation, reasoning parsing, budget tracking).
    /// </para>
    /// <para>
    /// The aggregated <see cref="TurnResult"/> is available via the last update's
    /// <c>AdditionalProperties</c> using <see cref="TurnResultKey"/>.
    /// </para>
    /// </remarks>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var sessionId = GetSessionId(options);

        // Enable reasoning in request if configured (applies to streaming too)
        var modifiedOptions = EnableReasoningIfRequired(options);

        // Inject conversation context if enabled
        var enrichedMessages = InjectConversationContext(sessionId, messageList);

        // Collect-and-Yield: buffer updates while streaming to caller
        var collectedUpdates = new List<ChatResponseUpdate>();

        await foreach (var update in base.GetStreamingResponseAsync(enrichedMessages, modifiedOptions, cancellationToken)
            .ConfigureAwait(false))
        {
            collectedUpdates.Add(update);
            yield return update;
        }

        // Post-stream orchestration: aggregate and process through turn manager
        if (collectedUpdates.Count > 0)
        {
            var aggregatedResponse = collectedUpdates.ToChatResponse();

            var context = CreateContext(enrichedMessages, modifiedOptions, cancellationToken, sessionId);

            var turnResult = await _turnManager.ProcessTurnAsync(
                context,
                (_, _) => Task.FromResult(aggregatedResponse));

            // Track conversation if enabled
            TrackConversation(sessionId, messageList, turnResult.Response);

            // Add telemetry tags
            AddTelemetryTags(turnResult);

            // Record metrics
            _meter?.RecordTurn(turnResult, sessionId);

            // Yield a final metadata-only update with the TurnResult
            var metadataUpdate = new ChatResponseUpdate
            {
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    [TurnResultKey] = turnResult
                }
            };

            if (_options.IncludeThinkingInMetadata && turnResult.ThinkingContent is not null)
            {
                metadataUpdate.AdditionalProperties[ThinkingContentKey] = turnResult.ThinkingContent;
            }

            if (_options.IncludeMetricsInMetadata)
            {
                metadataUpdate.AdditionalProperties[TurnMetricsKey] = turnResult.Metrics;
            }

            yield return metadataUpdate;
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
    /// Enables reasoning in the request if configured and required by the model.
    /// </summary>
    /// <param name="options">The original chat options.</param>
    /// <returns>Modified options with reasoning enabled, or the original options if not required.</returns>
    private ChatOptions? EnableReasoningIfRequired(ChatOptions? options)
    {
        // Skip if reasoning activation is not enabled
        if (!_options.EnableReasoning)
        {
            return options;
        }

        // Get model ID from options
        var modelId = options?.ModelId;

        // If auto-detection is enabled, check if the model requires explicit activation
        if (_options.AutoDetectReasoningRequirement)
        {
            var registry = _modifierRegistry ?? ReasoningRequestModifierRegistry.Default;
            if (!registry.RequiresExplicitActivation(modelId))
            {
                return options;
            }
        }

        // Find appropriate modifier and enable reasoning
        var modifier = GetReasoningModifier(modelId);
        if (modifier is null)
        {
            return options;
        }

        return modifier.EnableReasoning(options, modelId);
    }

    /// <summary>
    /// Gets the appropriate reasoning request modifier for the model.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The modifier if found; otherwise, null.</returns>
    private IReasoningRequestModifier? GetReasoningModifier(string? modelId)
    {
        // Use custom settings if provided
        if (_options.ReasoningRequestSettings is not null)
        {
            return new OpenSourceRequestModifier(_options.ReasoningRequestSettings);
        }

        // Use registry to find appropriate modifier
        var registry = _modifierRegistry ?? ReasoningRequestModifierRegistry.Default;
        return registry.GetByModel(modelId);
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
    /// Adds IndexThinking-specific telemetry tags to the current Activity.
    /// </summary>
    /// <remarks>
    /// These tags complement the OpenTelemetry GenAI semantic conventions
    /// by providing IndexThinking-specific metrics. Use with UseOpenTelemetry()
    /// in the chat client pipeline for full observability.
    /// </remarks>
    private static void AddTelemetryTags(TurnResult turnResult)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        // Add IndexThinking-specific tags using namespaced keys
        activity.SetTag("indexthinking.thinking_tokens", turnResult.Metrics.ThinkingTokens);
        activity.SetTag("indexthinking.continuation_count", turnResult.Metrics.ContinuationCount);
        activity.SetTag("indexthinking.truncation_detected", turnResult.WasTruncated);
        activity.SetTag("indexthinking.duration_ms", turnResult.Metrics.Duration.TotalMilliseconds);

        if (turnResult.Metrics.DetectedComplexity.HasValue)
        {
            activity.SetTag("indexthinking.complexity", turnResult.Metrics.DetectedComplexity.Value.ToString().ToLowerInvariant());
        }

        if (turnResult.HasThinkingContent)
        {
            activity.SetTag("indexthinking.has_thinking_content", true);
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
