using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Core;
using IndexThinking.Diagnostics;
using IndexThinking.Modifiers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace IndexThinking.Client;

/// <summary>
/// Extension methods for configuring <see cref="ThinkingChatClient"/> in a pipeline.
/// </summary>
public static class ThinkingChatClientExtensions
{
    /// <summary>
    /// Adds thinking orchestration to the chat client pipeline.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="turnManager">The turn manager for orchestrating thinking turns.</param>
    /// <param name="configure">Optional action to configure options.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseIndexThinking(
        this ChatClientBuilder builder,
        IThinkingTurnManager turnManager,
        Action<ThinkingChatClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(turnManager);

        return builder.Use(innerClient =>
        {
            var options = new ThinkingChatClientOptions();
            configure?.Invoke(options);
            return new ThinkingChatClient(innerClient, turnManager, options);
        });
    }

    /// <summary>
    /// Adds thinking orchestration to the chat client pipeline using DI.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="configure">Optional action to configure options.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload resolves services from the DI container:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="IThinkingTurnManager"/> (required — <c>AddIndexThinkingAgents()</c>)</item>
    ///   <item><see cref="ITokenCounter"/>, <see cref="IndexThinkingMeter"/>, <see cref="ReasoningRequestModifierRegistry"/> (optional)</item>
    /// </list>
    /// </remarks>
    public static ChatClientBuilder UseIndexThinking(
        this ChatClientBuilder builder,
        Action<ThinkingChatClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Use((innerClient, services) =>
        {
            var turnManager = services.GetRequiredService<IThinkingTurnManager>();
            var tokenCounter = services.GetService<ITokenCounter>();
            // What AddIndexThinkingMetrics() and AddIndexThinkingReasoningModifiers(…) registered. A registry does not
            // turn reasoning on by itself — ThinkingChatClientOptions.EnableReasoning still decides.
            var meter = services.GetService<IndexThinkingMeter>();
            var modifierRegistry = services.GetService<ReasoningRequestModifierRegistry>();

            var options = new ThinkingChatClientOptions();
            configure?.Invoke(options);

            return new ThinkingChatClient(innerClient, turnManager, options,
                meter, modifierRegistry, tokenCounter);
        });
    }

    /// <summary>
    /// Gets the <see cref="ThinkingContent"/> from a response, if present.
    /// </summary>
    /// <param name="response">The chat response.</param>
    /// <returns>The thinking content, or null if not present.</returns>
    public static ThinkingContent? GetThinkingContent(this ChatResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.AdditionalProperties?.TryGetValue(ThinkingChatClient.ThinkingContentKey, out var content) == true)
        {
            return content as ThinkingContent;
        }

        return null;
    }

    /// <summary>
    /// Gets the <see cref="TurnMetrics"/> from a response, if present.
    /// </summary>
    /// <param name="response">The chat response.</param>
    /// <returns>The turn metrics, or null if not present.</returns>
    public static TurnMetrics? GetTurnMetrics(this ChatResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.AdditionalProperties?.TryGetValue(ThinkingChatClient.TurnMetricsKey, out var metrics) == true)
        {
            return metrics as TurnMetrics;
        }

        return null;
    }

    /// <summary>
    /// Gets the full <see cref="TurnResult"/> from a response, if present.
    /// </summary>
    /// <param name="response">The chat response.</param>
    /// <returns>The turn result, or null if not present.</returns>
    public static TurnResult? GetTurnResult(this ChatResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.AdditionalProperties?.TryGetValue(ThinkingChatClient.TurnResultKey, out var result) == true)
        {
            return result as TurnResult;
        }

        return null;
    }

    // ========================================
    // Session id (keys the thinking state store and metrics)
    // ========================================

    /// <summary>
    /// Session ID key used in ChatOptions.AdditionalProperties. The session id keys the thinking state
    /// (<see cref="IThinkingStateStore"/>) and the per-session metrics; the conversation history stays the caller's.
    /// </summary>
    public const string SessionIdKey = "IndexThinking.SessionId";

    /// <summary>
    /// Creates chat options with the specified session ID.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>Chat options with session ID configured.</returns>
    public static ChatOptions WithSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [SessionIdKey] = sessionId
            }
        };
    }

    /// <summary>
    /// Adds a session ID to existing chat options.
    /// </summary>
    /// <param name="options">Existing options (may be null).</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>Chat options with session ID configured.</returns>
    public static ChatOptions WithSessionId(ChatOptions? options, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        options ??= new ChatOptions();
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties[SessionIdKey] = sessionId;

        return options;
    }
}
