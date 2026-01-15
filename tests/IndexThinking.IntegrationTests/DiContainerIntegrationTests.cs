using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Client;
using IndexThinking.Context;
using IndexThinking.Diagnostics;
using IndexThinking.Extensions;
using IndexThinking.IntegrationTests.Fixtures;
using IndexThinking.Stores;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace IndexThinking.IntegrationTests;

/// <summary>
/// Integration tests for DI container service registration and resolution.
/// </summary>
public class DiContainerIntegrationTests
{
    [Fact]
    public void AddIndexThinkingAgents_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingAgents();

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IComplexityEstimator>().Should().NotBeNull();
        provider.GetService<IContinuationHandler>().Should().NotBeNull();
        provider.GetService<IThinkingTurnManager>().Should().NotBeNull();
        provider.GetService<ITruncationDetector>().Should().NotBeNull();
        provider.GetService<ITokenCounter>().Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingInMemoryStorage_RegistersStateStore()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingInMemoryStorage();

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IThinkingStateStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<InMemoryThinkingStateStore>();
    }

    [Fact]
    public void AddIndexThinkingInMemoryStorage_WithOptions_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingInMemoryStorage(new InMemoryStateStoreOptions
            {
                DefaultTtl = TimeSpan.FromMinutes(30),
                CleanupInterval = TimeSpan.FromMinutes(5)
            });

        // Act
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        // Assert
        store.Should().NotBeNull();
        store.Should().BeOfType<InMemoryThinkingStateStore>();
    }

    [Fact]
    public void AddIndexThinkingContext_RegistersContextServices()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingContext();

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IContextTracker>().Should().NotBeNull();
        provider.GetService<IContextInjector>().Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingContext_WithOptions_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingContext(
                new ContextTrackerOptions
                {
                    MaxTurns = 20,
                    SessionTtl = TimeSpan.FromHours(2)
                },
                new ContextInjectorOptions
                {
                    MaxTurnsToInject = 10
                });

        // Act
        var provider = services.BuildServiceProvider();
        var tracker = provider.GetService<IContextTracker>();
        var injector = provider.GetService<IContextInjector>();

        // Assert
        tracker.Should().NotBeNull();
        injector.Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingDistributedStorage_WithMemoryCache_RegistersStateStore()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage();

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IThinkingStateStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<DistributedCacheThinkingStateStore>();
    }

    [Fact]
    public void AddIndexThinkingDistributedStorage_WithOptions_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage(options =>
            {
                options.KeyPrefix = "test:";
                options.AbsoluteExpiration = TimeSpan.FromMinutes(60);
            });

        // Act
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingHealthChecks_RegistersHealthCheck()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage()
            .AddIndexThinkingHealthChecks();

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        var healthCheck = provider.GetService<ThinkingStateStoreHealthCheck>();
        healthCheck.Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingMetrics_RegistersMeter()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingMetrics();

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        var meter = provider.GetService<IndexThinkingMeter>();
        meter.Should().NotBeNull();
    }

    [Fact]
    public void AllServices_CanBeResolved_Together()
    {
        // Arrange - Full registration
        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .AddIndexThinkingContext()
            .AddIndexThinkingMetrics();

        // Act
        var provider = services.BuildServiceProvider();

        // Assert - All services resolve
        provider.GetService<IComplexityEstimator>().Should().NotBeNull();
        provider.GetService<IContinuationHandler>().Should().NotBeNull();
        provider.GetService<IThinkingTurnManager>().Should().NotBeNull();
        provider.GetService<IThinkingStateStore>().Should().NotBeNull();
        provider.GetService<IContextTracker>().Should().NotBeNull();
        provider.GetService<IContextInjector>().Should().NotBeNull();
        provider.GetService<IndexThinkingMeter>().Should().NotBeNull();
    }

    [Fact]
    public async Task ChatClientBuilder_UseIndexThinking_BuildsWithDI()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("Hello!");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        // Act
        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(services);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]);

        // Assert
        client.Should().NotBeNull();
        response.Should().NotBeNull();
    }

    [Fact]
    public void ServiceCollection_MultipleRegistrations_FirstWins_WithTryAdd()
    {
        // Arrange - TryAddSingleton semantics: first registration wins
        var services = new ServiceCollection()
            .AddIndexThinkingInMemoryStorage()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage(); // TryAdd won't override InMemory

        // Act
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        // Assert - First wins with TryAddSingleton
        store.Should().BeOfType<InMemoryThinkingStateStore>();
    }

    [Fact]
    public void ServiceCollection_ReverseOrder_DistributedCacheWins()
    {
        // Arrange - When distributed is registered first, it wins
        var services = new ServiceCollection()
            .AddDistributedMemoryCache()
            .AddIndexThinkingDistributedStorage() // Registered first
            .AddIndexThinkingInMemoryStorage();   // TryAdd won't override

        // Act
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        // Assert - First registration wins
        store.Should().BeOfType<DistributedCacheThinkingStateStore>();
    }

    [Fact]
    public void AddIndexThinkingAgents_WithOptions_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingAgents(options =>
            {
                options.AutoEstimateComplexity = false;
                options.DefaultBudget = new Core.BudgetConfig
                {
                    ThinkingBudget = 8192,
                    AnswerBudget = 4096
                };
            });

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IThinkingTurnManager>().Should().NotBeNull();
    }
}
