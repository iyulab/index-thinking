using FluentAssertions;
using IndexThinking.Core;
using IndexThinking.Stores;
using Xunit;

namespace IndexThinking.Tests.Stores;

/// <summary>
/// Tests for <see cref="InMemoryThinkingStateStore"/> inheriting common tests.
/// </summary>
public class InMemoryThinkingStateStoreTests : ThinkingStateStoreTestsBase<InMemoryThinkingStateStore>
{
    protected override InMemoryThinkingStateStore CreateStore()
    {
        return new InMemoryThinkingStateStore();
    }

    #region InMemory-Specific Tests

    [Fact]
    public async Task Count_ShouldReflectStoredItems()
    {
        // Arrange
        Store.Count.Should().Be(0);

        await Store.SetAsync("s1", new ThinkingState { SessionId = "s1" });
        await Store.SetAsync("s2", new ThinkingState { SessionId = "s2" });

        // Assert
        Store.Count.Should().Be(2);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllStates()
    {
        // Arrange
        await Store.SetAsync("s1", new ThinkingState { SessionId = "s1" });
        await Store.SetAsync("s2", new ThinkingState { SessionId = "s2" });

        // Act
        Store.Clear();

        // Assert
        Store.Count.Should().Be(0);
        (await Store.ExistsAsync("s1")).Should().BeFalse();
        (await Store.ExistsAsync("s2")).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var sessionIds = Enumerable.Range(1, 100).Select(i => $"session-{i}").ToList();

        // Act - concurrent writes
        foreach (var sessionId in sessionIds)
        {
            tasks.Add(Task.Run(async () =>
            {
                await Store.SetAsync(sessionId, new ThinkingState { SessionId = sessionId });
            }));
        }
        await Task.WhenAll(tasks);

        // Assert
        Store.Count.Should().Be(100);

        // Verify all can be read
        foreach (var sessionId in sessionIds)
        {
            var result = await Store.GetAsync(sessionId);
            result.Should().NotBeNull();
            result!.SessionId.Should().Be(sessionId);
        }
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="InMemoryThinkingStateStore"/> with TTL enabled.
/// </summary>
public class InMemoryThinkingStateStoreWithTtlTests : IDisposable
{
    private readonly InMemoryThinkingStateStore _store;

    public InMemoryThinkingStateStoreWithTtlTests()
    {
        var options = new InMemoryStateStoreOptions
        {
            DefaultTtl = TimeSpan.FromMilliseconds(100),
            UseSlidingExpiration = false
        };
        _store = new InMemoryThinkingStateStore(options);
    }

    public void Dispose()
    {
        _store.Dispose();
    }

    [Fact]
    public async Task GetAsync_ExpiredEntry_ShouldReturnNull()
    {
        // Arrange
        await _store.SetAsync("session-1", new ThinkingState { SessionId = "session-1" });

        // Wait for expiration
        await Task.Delay(150);

        // Act
        var result = await _store.GetAsync("session-1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ExpiredEntry_ShouldReturnFalse()
    {
        // Arrange
        await _store.SetAsync("session-1", new ThinkingState { SessionId = "session-1" });

        // Wait for expiration
        await Task.Delay(150);

        // Act
        var result = await _store.ExistsAsync("session-1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_NotExpired_ShouldReturnState()
    {
        // Arrange
        await _store.SetAsync("session-1", new ThinkingState { SessionId = "session-1" });

        // Act immediately (before expiration)
        var result = await _store.GetAsync("session-1");

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be("session-1");
    }

    [Fact]
    public async Task CleanupExpired_ShouldRemoveExpiredEntries()
    {
        // Arrange
        await _store.SetAsync("session-1", new ThinkingState { SessionId = "session-1" });
        await _store.SetAsync("session-2", new ThinkingState { SessionId = "session-2" });

        _store.Count.Should().Be(2);

        // Wait for expiration
        await Task.Delay(150);

        // Act
        var removed = _store.CleanupExpired();

        // Assert
        removed.Should().Be(2);
        _store.Count.Should().Be(0);
    }
}

/// <summary>
/// Tests for <see cref="InMemoryThinkingStateStore"/> with sliding expiration.
/// </summary>
public class InMemoryThinkingStateStoreSlidingExpirationTests : IDisposable
{
    private readonly InMemoryThinkingStateStore _store;

    public InMemoryThinkingStateStoreSlidingExpirationTests()
    {
        // Use longer TTL to avoid timer resolution issues on Windows (~15.6ms)
        var options = new InMemoryStateStoreOptions
        {
            DefaultTtl = TimeSpan.FromMilliseconds(500),
            UseSlidingExpiration = true
        };
        _store = new InMemoryThinkingStateStore(options);
    }

    public void Dispose()
    {
        _store.Dispose();
    }

    [Fact]
    public async Task GetAsync_WithSlidingExpiration_ShouldExtendTtl()
    {
        // Arrange
        await _store.SetAsync("session-1", new ThinkingState { SessionId = "session-1" });

        // Access multiple times before expiration to extend TTL
        for (int i = 0; i < 3; i++)
        {
            await Task.Delay(150); // Less than 500ms TTL
            var result = await _store.GetAsync("session-1");
            result.Should().NotBeNull($"iteration {i}");
        }

        // Wait just under TTL again
        await Task.Delay(150);
        var finalResult = await _store.GetAsync("session-1");

        // Assert - should still exist due to sliding expiration
        finalResult.Should().NotBeNull();
    }
}

/// <summary>
/// Tests for <see cref="InMemoryStateStoreOptions"/>.
/// </summary>
public class InMemoryStateStoreOptionsTests
{
    [Fact]
    public void NoExpiration_ShouldHaveNullTtl()
    {
        // Act
        var options = InMemoryStateStoreOptions.NoExpiration;

        // Assert
        options.DefaultTtl.Should().BeNull();
        options.UseSlidingExpiration.Should().BeFalse();
        options.CleanupInterval.Should().BeNull();
    }

    [Fact]
    public void WithTtl_ShouldSetTtlAndOptionalCleanup()
    {
        // Act
        var options = InMemoryStateStoreOptions.WithTtl(TimeSpan.FromMinutes(5));

        // Assert
        options.DefaultTtl.Should().Be(TimeSpan.FromMinutes(5));
        options.CleanupInterval.Should().BeNull();
    }

    [Fact]
    public void WithTtl_AndCleanupInterval_ShouldSetBoth()
    {
        // Act
        var options = InMemoryStateStoreOptions.WithTtl(
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1));

        // Assert
        options.DefaultTtl.Should().Be(TimeSpan.FromMinutes(5));
        options.CleanupInterval.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Options_SupportsInitSyntax()
    {
        // Act
        var options = new InMemoryStateStoreOptions
        {
            DefaultTtl = TimeSpan.FromHours(1),
            UseSlidingExpiration = true,
            CleanupInterval = TimeSpan.FromMinutes(10)
        };

        // Assert
        options.DefaultTtl.Should().Be(TimeSpan.FromHours(1));
        options.UseSlidingExpiration.Should().BeTrue();
        options.CleanupInterval.Should().Be(TimeSpan.FromMinutes(10));
    }
}

/// <summary>
/// Tests for disposed <see cref="InMemoryThinkingStateStore"/>.
/// </summary>
public class InMemoryThinkingStateStoreDisposeTests
{
    [Fact]
    public async Task GetAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var store = new InMemoryThinkingStateStore();
        store.Dispose();

        // Act
        var action = () => store.GetAsync("session-1");

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SetAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var store = new InMemoryThinkingStateStore();
        store.Dispose();

        // Act
        var action = () => store.SetAsync("session-1", new ThinkingState { SessionId = "session-1" });

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var store = new InMemoryThinkingStateStore();

        // Act
        var action = () =>
        {
            store.Dispose();
            store.Dispose();
            store.Dispose();
        };

        // Assert
        action.Should().NotThrow();
    }
}
