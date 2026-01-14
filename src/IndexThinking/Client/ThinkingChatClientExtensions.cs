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
    /// This overload resolves <see cref="IThinkingTurnManager"/> from the service provider.
    /// Ensure that <see cref="Extensions.ServiceCollectionExtensions.AddIndexThinkingAgents"/>
    /// has been called to register the required services.
    /// </remarks>
    public static ChatClientBuilder UseIndexThinking(
        this ChatClientBuilder builder,
        Action<ThinkingChatClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Use((innerClient, services) =>
        {
            var turnManager = services.GetRequiredService<IThinkingTurnManager>();
            var options = new ThinkingChatClientOptions();
            configure?.Invoke(options);
            return new ThinkingChatClient(innerClient, turnManager, options);
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
}
