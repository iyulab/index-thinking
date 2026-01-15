using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Core;
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
    /// <param name="contextTracker">Optional context tracker for conversation management.</param>
    /// <param name="contextInjector">Optional context injector for message enrichment.</param>
    /// <returns>The builder for chaining.</returns>
    public static ChatClientBuilder UseIndexThinking(
        this ChatClientBuilder builder,
        IThinkingTurnManager turnManager,
        Action<ThinkingChatClientOptions>? configure = null,
        IContextTracker? contextTracker = null,
        IContextInjector? contextInjector = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(turnManager);

        return builder.Use(innerClient =>
        {
            var options = new ThinkingChatClientOptions();
            configure?.Invoke(options);
            return new ThinkingChatClient(innerClient, turnManager, options, contextTracker, contextInjector);
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
    ///   <item><see cref="IThinkingTurnManager"/> (required)</item>
    ///   <item><see cref="IContextTracker"/> (optional, for conversation tracking)</item>
    ///   <item><see cref="IContextInjector"/> (optional, for context injection)</item>
    /// </list>
    /// <para>
    /// Ensure that the required services are registered using:
    /// AddIndexThinkingAgents() for turn management and
    /// AddIndexThinkingContext() for context tracking.
    /// </para>
    /// </remarks>
    public static ChatClientBuilder UseIndexThinking(
        this ChatClientBuilder builder,
        Action<ThinkingChatClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Use((innerClient, services) =>
        {
            var turnManager = services.GetRequiredService<IThinkingTurnManager>();
            var contextTracker = services.GetService<IContextTracker>();
            var contextInjector = services.GetService<IContextInjector>();

            var options = new ThinkingChatClientOptions();
            configure?.Invoke(options);

            return new ThinkingChatClient(innerClient, turnManager, options, contextTracker, contextInjector);
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
    // Convenience Extension Methods (v0.9.0)
    // ========================================

    /// <summary>
    /// Session ID key used in ChatOptions.AdditionalProperties.
    /// </summary>
    public const string SessionIdKey = "IndexThinking.SessionId";

    /// <summary>
    /// Sends a simple message with session tracking.
    /// </summary>
    /// <param name="client">The chat client.</param>
    /// <param name="sessionId">Session identifier for conversation tracking.</param>
    /// <param name="message">The user message.</param>
    /// <param name="options">Optional chat options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assistant's response text.</returns>
    /// <remarks>
    /// This is a convenience method for simple chat scenarios.
    /// For more control, use <see cref="IChatClient.GetResponseAsync"/> directly.
    /// </remarks>
    public static async Task<string> ChatAsync(
        this IChatClient client,
        string sessionId,
        string message,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        options = WithSessionId(options, sessionId);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, message)],
            options,
            cancellationToken);

        return response.Messages?.FirstOrDefault()?.Text ?? string.Empty;
    }

    /// <summary>
    /// Sends a message with session tracking and returns the full response.
    /// </summary>
    /// <param name="client">The chat client.</param>
    /// <param name="sessionId">Session identifier for conversation tracking.</param>
    /// <param name="message">The user message.</param>
    /// <param name="options">Optional chat options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full chat response.</returns>
    public static Task<ChatResponse> SendAsync(
        this IChatClient client,
        string sessionId,
        string message,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        options = WithSessionId(options, sessionId);

        return client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, message)],
            options,
            cancellationToken);
    }

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
