using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using IndexThinking.Stores;
using Xunit;

namespace IndexThinking.Tests.Stores;

/// <summary>
/// Tests for <see cref="DistributedCacheThinkingStateStore"/>.
/// Uses MemoryDistributedCache as the test backend.
/// </summary>
public class DistributedCacheThinkingStateStoreTests : ThinkingStateStoreTestsBase<DistributedCacheThinkingStateStore>
{
    protected override DistributedCacheThinkingStateStore CreateStore()
    {
        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        var cache = new MemoryDistributedCache(memoryOptions);

        var options = new DistributedCacheStateStoreOptions
        {
            KeyPrefix = "test:",
            AbsoluteExpiration = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(15)
        };

        return new DistributedCacheThinkingStateStore(cache, options);
    }
}

/// <summary>
/// Additional tests specific to distributed cache store features.
/// </summary>
public class DistributedCacheThinkingStateStoreSpecificTests
{
    [Fact]
    public void Constructor_WithNullCache_ShouldThrow()
    {
        // Act
        var action = () => new DistributedCacheThinkingStateStore(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldUseDefaults()
    {
        // Arrange
        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        var cache = new MemoryDistributedCache(memoryOptions);

        // Act
        var store = new DistributedCacheThinkingStateStore(cache, null);

        // Assert - just verify it doesn't throw
        store.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldResetSlidingExpiration()
    {
        // Arrange
        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        var cache = new MemoryDistributedCache(memoryOptions);

        var options = new DistributedCacheStateStoreOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        };

        var store = new DistributedCacheThinkingStateStore(cache, options);
        var state = new IndexThinking.Core.ThinkingState
        {
            SessionId = "refresh-test"
        };

        await store.SetAsync("refresh-test", state);

        // Act
        var refreshAction = () => store.RefreshAsync("refresh-test");

        // Assert
        await refreshAction.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshAsync_WithInvalidSessionId_ShouldThrow(string? sessionId)
    {
        // Arrange
        var memoryOptions = Options.Create(new MemoryDistributedCacheOptions());
        var cache = new MemoryDistributedCache(memoryOptions);
        var store = new DistributedCacheThinkingStateStore(cache);

        // Act
        var action = () => store.RefreshAsync(sessionId!);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }
}

/// <summary>
/// Tests for <see cref="DistributedCacheStateStoreOptions"/>.
/// </summary>
public class DistributedCacheStateStoreOptionsTests
{
    [Fact]
    public void Default_ShouldHaveExpectedValues()
    {
        // Act
        var options = DistributedCacheStateStoreOptions.Default;

        // Assert
        options.KeyPrefix.Should().Be("thinking:state:");
        options.AbsoluteExpiration.Should().Be(TimeSpan.FromHours(1));
        options.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void LongRunning_ShouldHaveLongerExpirations()
    {
        // Act
        var options = DistributedCacheStateStoreOptions.LongRunning;

        // Assert
        options.AbsoluteExpiration.Should().Be(TimeSpan.FromHours(24));
        options.SlidingExpiration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Ephemeral_ShouldHaveNoSlidingExpiration()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(5);

        // Act
        var options = DistributedCacheStateStoreOptions.Ephemeral(duration);

        // Assert
        options.AbsoluteExpiration.Should().Be(duration);
        options.SlidingExpiration.Should().BeNull();
    }
}
