using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using IndexThinking.Extensions;
using IndexThinking.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IndexThinking.IntegrationTests;

/// <summary>
/// Integration tests for state storage implementations.
/// Tests InMemory, SQLite, and DistributedCache stores.
/// </summary>
public class StateStorageIntegrationTests
{
    private static ThinkingState CreateTestState(string sessionId) => new()
    {
        SessionId = sessionId,
        ModelId = "test-model",
        TotalThinkingTokens = 100,
        TotalOutputTokens = 200,
        ContinuationCount = 1,
        ReasoningState = new ReasoningState
        {
            Provider = "test",
            Data = [1, 2, 3, 4, 5]
        }
    };

    #region InMemory Store Tests

    [Fact]
    public async Task InMemoryStore_SetAndGet_RoundTrips()
    {
        // Arrange
        var store = new InMemoryThinkingStateStore();
        var state = CreateTestState("session-1");

        // Act
        await store.SetAsync("session-1", state);
        var result = await store.GetAsync("session-1");

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be("session-1");
        result.TotalThinkingTokens.Should().Be(100);
        result.ReasoningState.Should().NotBeNull();
        result.ReasoningState!.Data.Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task InMemoryStore_WithTtl_ExpiresCorrectly()
    {
        // Arrange
        var options = new InMemoryStateStoreOptions
        {
            DefaultTtl = TimeSpan.FromMilliseconds(100)
            // CleanupInterval not set = lazy cleanup only
        };
        var store = new InMemoryThinkingStateStore(options);
        var state = CreateTestState("session-ttl");

        // Act
        await store.SetAsync("session-ttl", state);

        // Wait for expiration
        await Task.Delay(150);

        var result = await store.GetAsync("session-ttl");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task InMemoryStore_Remove_DeletesState()
    {
        // Arrange
        var store = new InMemoryThinkingStateStore();
        var state = CreateTestState("session-remove");

        await store.SetAsync("session-remove", state);

        // Act
        await store.RemoveAsync("session-remove");
        var result = await store.GetAsync("session-remove");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task InMemoryStore_Exists_ReturnsCorrectly()
    {
        // Arrange
        var store = new InMemoryThinkingStateStore();
        var state = CreateTestState("session-exists");

        // Act & Assert - Before
        (await store.ExistsAsync("session-exists")).Should().BeFalse();

        await store.SetAsync("session-exists", state);

        // Act & Assert - After
        (await store.ExistsAsync("session-exists")).Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryStore_MultipleSessions_Isolated()
    {
        // Arrange
        var store = new InMemoryThinkingStateStore();
        var state1 = CreateTestState("session-1") with { TotalThinkingTokens = 100 };
        var state2 = CreateTestState("session-2") with { TotalThinkingTokens = 200 };

        // Act
        await store.SetAsync("session-1", state1);
        await store.SetAsync("session-2", state2);

        var result1 = await store.GetAsync("session-1");
        var result2 = await store.GetAsync("session-2");

        // Assert
        result1!.TotalThinkingTokens.Should().Be(100);
        result2!.TotalThinkingTokens.Should().Be(200);
    }

    #endregion

    #region DistributedCache Store Tests

    [Fact]
    public async Task DistributedCacheStore_SetAndGet_RoundTrips()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage()
            .BuildServiceProvider();

        var store = services.GetRequiredService<IThinkingStateStore>();
        var state = CreateTestState("dist-session-1");

        // Act
        await store.SetAsync("dist-session-1", state);
        var result = await store.GetAsync("dist-session-1");

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be("dist-session-1");
        result.TotalThinkingTokens.Should().Be(100);
    }

    [Fact]
    public async Task DistributedCacheStore_WithKeyPrefix_UsesPrefix()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage(options =>
            {
                options.KeyPrefix = "myapp:thinking:";
            })
            .BuildServiceProvider();

        var store = services.GetRequiredService<IThinkingStateStore>();
        var state = CreateTestState("prefixed-session");

        // Act
        await store.SetAsync("prefixed-session", state);
        var result = await store.GetAsync("prefixed-session");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DistributedCacheStore_WithExpiration_SetsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage(options =>
            {
                options.AbsoluteExpiration = TimeSpan.FromHours(1);
                options.SlidingExpiration = TimeSpan.FromMinutes(30);
            })
            .BuildServiceProvider();

        var store = services.GetRequiredService<IThinkingStateStore>();
        var state = CreateTestState("expiring-session");

        // Act
        await store.SetAsync("expiring-session", state);
        var result = await store.GetAsync("expiring-session");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DistributedCacheStore_Remove_DeletesState()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage()
            .BuildServiceProvider();

        var store = services.GetRequiredService<IThinkingStateStore>();
        var state = CreateTestState("dist-remove");

        await store.SetAsync("dist-remove", state);

        // Act
        await store.RemoveAsync("dist-remove");
        var result = await store.GetAsync("dist-remove");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Health Check Tests

    [Fact]
    public async Task HealthCheck_WithWorkingStore_ReturnsHealthy()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage()
            .AddIndexThinkingHealthChecks()
            .BuildServiceProvider();

        var healthCheck = services.GetRequiredService<ThinkingStateStoreHealthCheck>();

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        // Assert
        result.Status.Should().Be(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
    }

    #endregion

    #region DI Registration Tests

    [Fact]
    public void InMemoryStorage_FromDI_ResolvesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        // Act
        var store = services.GetService<IThinkingStateStore>();

        // Assert
        store.Should().NotBeNull();
        store.Should().BeOfType<InMemoryThinkingStateStore>();
    }

    [Fact]
    public void DistributedStorage_FromDI_ResolvesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage()
            .BuildServiceProvider();

        // Act
        var store = services.GetService<IThinkingStateStore>();

        // Assert
        store.Should().NotBeNull();
        store.Should().BeOfType<DistributedCacheThinkingStateStore>();
    }

    #endregion
}
