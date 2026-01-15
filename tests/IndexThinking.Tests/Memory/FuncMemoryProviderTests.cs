using FluentAssertions;
using IndexThinking.Memory;
using Xunit;

namespace IndexThinking.Tests.Memory;

public class FuncMemoryProviderTests
{
    [Fact]
    public void Constructor_NullDelegate_Throws()
    {
        // Act
        var action = () => new FuncMemoryProvider(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsConfigured_ReturnsTrue()
    {
        // Arrange
        var provider = new FuncMemoryProvider((_, _, _, _, _) => Task.FromResult(MemoryRecallResult.Empty));

        // Act & Assert
        provider.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task RecallAsync_CallsDelegate()
    {
        // Arrange
        var called = false;
        string? capturedUserId = null;
        string? capturedSessionId = null;
        string? capturedQuery = null;
        int capturedLimit = 0;

        var provider = new FuncMemoryProvider((userId, sessionId, query, limit, ct) =>
        {
            called = true;
            capturedUserId = userId;
            capturedSessionId = sessionId;
            capturedQuery = query;
            capturedLimit = limit;
            return Task.FromResult(MemoryRecallResult.Empty);
        });

        // Act
        await provider.RecallAsync("user-1", "session-1", "test query", 15);

        // Assert
        called.Should().BeTrue();
        capturedUserId.Should().Be("user-1");
        capturedSessionId.Should().Be("session-1");
        capturedQuery.Should().Be("test query");
        capturedLimit.Should().Be(15);
    }

    [Fact]
    public async Task RecallAsync_ConvertsUserMemories()
    {
        // Arrange
        var provider = new FuncMemoryProvider((_, _, _, _, _) =>
        {
            return Task.FromResult(new MemoryRecallResult
            {
                UserMemories = [("user fact 1", 0.9f), ("user fact 2", 0.8f)]
            });
        });

        // Act
        var result = await provider.RecallAsync("user-1", null, "query");

        // Assert
        result.UserMemories.Should().HaveCount(2);
        result.UserMemories[0].Content.Should().Be("user fact 1");
        result.UserMemories[0].Scope.Should().Be("user");
        result.UserMemories[0].Relevance.Should().Be(0.9f);
        result.UserMemories[1].Content.Should().Be("user fact 2");
        result.UserMemories[1].Relevance.Should().Be(0.8f);
    }

    [Fact]
    public async Task RecallAsync_ConvertsSessionMemories()
    {
        // Arrange
        var provider = new FuncMemoryProvider((_, _, _, _, _) =>
        {
            return Task.FromResult(new MemoryRecallResult
            {
                SessionMemories = [("session context", 0.95f)]
            });
        });

        // Act
        var result = await provider.RecallAsync("user-1", "session-1", "query");

        // Assert
        result.SessionMemories.Should().HaveCount(1);
        result.SessionMemories[0].Content.Should().Be("session context");
        result.SessionMemories[0].Scope.Should().Be("session");
        result.SessionMemories[0].Relevance.Should().Be(0.95f);
    }

    [Fact]
    public async Task RecallAsync_ConvertsTopicMemories()
    {
        // Arrange
        var provider = new FuncMemoryProvider((_, _, _, _, _) =>
        {
            return Task.FromResult(new MemoryRecallResult
            {
                TopicMemories = [("topic info", null)]
            });
        });

        // Act
        var result = await provider.RecallAsync("user-1", "session-1", "query");

        // Assert
        result.TopicMemories.Should().HaveCount(1);
        result.TopicMemories[0].Content.Should().Be("topic info");
        result.TopicMemories[0].Scope.Should().Be("topic");
        result.TopicMemories[0].Relevance.Should().BeNull();
    }

    [Fact]
    public async Task RecallAsync_CombinesAllMemoriesIntoAll()
    {
        // Arrange
        var provider = new FuncMemoryProvider((_, _, _, _, _) =>
        {
            return Task.FromResult(new MemoryRecallResult
            {
                UserMemories = [("user 1", 0.9f)],
                SessionMemories = [("session 1", 0.8f)],
                TopicMemories = [("topic 1", 0.7f)]
            });
        });

        // Act
        var result = await provider.RecallAsync("user-1", "session-1", "query");

        // Assert
        result.Memories.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
        result.HasMemories.Should().BeTrue();
    }

    [Fact]
    public async Task RecallAsync_PreservesQuery()
    {
        // Arrange
        var provider = new FuncMemoryProvider((_, _, _, _, _) =>
        {
            return Task.FromResult(MemoryRecallResult.Empty);
        });

        // Act
        var result = await provider.RecallAsync("user-1", null, "my query");

        // Assert
        result.Query.Should().Be("my query");
    }

    [Fact]
    public async Task RecallAsync_PassesCancellationToken()
    {
        // Arrange
        CancellationToken capturedToken = default;
        var provider = new FuncMemoryProvider((_, _, _, _, ct) =>
        {
            capturedToken = ct;
            return Task.FromResult(MemoryRecallResult.Empty);
        });
        using var cts = new CancellationTokenSource();

        // Act
        await provider.RecallAsync("user-1", null, "query", cancellationToken: cts.Token);

        // Assert
        capturedToken.Should().Be(cts.Token);
    }
}
