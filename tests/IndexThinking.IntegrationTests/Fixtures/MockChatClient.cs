using Microsoft.Extensions.AI;

namespace IndexThinking.IntegrationTests.Fixtures;

/// <summary>
/// A configurable mock IChatClient for integration testing.
/// Unlike Moq mocks, this allows complex behavior configuration.
/// </summary>
public class MockChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new();
    private readonly List<IList<ChatMessage>> _receivedMessages = new();
    private ChatFinishReason _finishReason = ChatFinishReason.Stop;
    private int _callCount;
    private Func<IEnumerable<ChatMessage>, ChatResponse>? _responseFactory;

    public ChatClientMetadata Metadata { get; set; } = new("mock", new Uri("http://mock"), "mock-model");
    public IReadOnlyList<IList<ChatMessage>> ReceivedMessages => _receivedMessages;
    public int CallCount => _callCount;

    /// <summary>
    /// Configures the next response to return.
    /// </summary>
    public MockChatClient WithResponse(string content, ChatFinishReason? finishReason = null)
    {
        var response = CreateResponse(content, finishReason ?? _finishReason);
        _responses.Enqueue(response);
        return this;
    }

    /// <summary>
    /// Configures multiple responses for continuation testing.
    /// </summary>
    public MockChatClient WithResponses(params (string content, ChatFinishReason reason)[] responses)
    {
        foreach (var (content, reason) in responses)
        {
            _responses.Enqueue(CreateResponse(content, reason));
        }
        return this;
    }

    /// <summary>
    /// Configures a response factory for dynamic responses.
    /// </summary>
    public MockChatClient WithResponseFactory(Func<IEnumerable<ChatMessage>, ChatResponse> factory)
    {
        _responseFactory = factory;
        return this;
    }

    /// <summary>
    /// Configures default finish reason for responses without explicit reason.
    /// </summary>
    public MockChatClient WithDefaultFinishReason(ChatFinishReason reason)
    {
        _finishReason = reason;
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _callCount++;
        _receivedMessages.Add(messages.ToList());

        if (_responseFactory is not null)
        {
            return Task.FromResult(_responseFactory(messages));
        }

        if (_responses.Count > 0)
        {
            return Task.FromResult(_responses.Dequeue());
        }

        return Task.FromResult(CreateResponse("Default response", _finishReason));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GetStreamingUpdates(messages, cancellationToken);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> GetStreamingUpdates(
        IEnumerable<ChatMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await GetResponseAsync(messages, null, cancellationToken);
        var text = response.Messages.FirstOrDefault()?.Text ?? "";
        var words = text.Split(' ');

        for (var i = 0; i < words.Length; i++)
        {
            var isLast = i == words.Length - 1;
            var update = new ChatResponseUpdate
            {
                Contents = [new TextContent(words[i] + " ")]
            };

            // Include FinishReason and Usage on the last chunk
            if (isLast)
            {
                update.FinishReason = response.FinishReason;
                update.Contents.Add(new UsageContent(response.Usage ?? new UsageDetails()));
            }

            yield return update;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IChatClient))
            return this;
        return null;
    }

    private ChatResponse CreateResponse(string content, ChatFinishReason finishReason)
    {
        var message = new ChatMessage(ChatRole.Assistant, content);
        return new ChatResponse([message])
        {
            FinishReason = finishReason,
            Usage = new UsageDetails
            {
                InputTokenCount = 10,
                OutputTokenCount = content.Length / 4
            }
        };
    }
}
