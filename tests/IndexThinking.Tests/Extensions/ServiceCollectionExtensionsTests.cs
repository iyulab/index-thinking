using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Continuation;
using IndexThinking.Core;
using IndexThinking.Extensions;
using IndexThinking.Stores;
using NSubstitute;
using Xunit;

namespace IndexThinking.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    #region InMemory Storage

    [Fact]
    public void AddIndexThinkingInMemoryStorage_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIndexThinkingInMemoryStorage();

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        store.Should().NotBeNull();
        store.Should().BeOfType<InMemoryThinkingStateStore>();
    }

    [Fact]
    public void AddIndexThinkingInMemoryStorage_WithOptions_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new InMemoryStateStoreOptions
        {
            DefaultTtl = TimeSpan.FromMinutes(30)
        };

        // Act
        services.AddIndexThinkingInMemoryStorage(options);

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();
        var registeredOptions = provider.GetService<InMemoryStateStoreOptions>();

        store.Should().NotBeNull();
        registeredOptions.Should().NotBeNull();
        registeredOptions!.DefaultTtl.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void AddIndexThinkingInMemoryStorage_WithConfigure_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIndexThinkingInMemoryStorage(options =>
        {
            // Note: options is a new instance, we can set properties
            // but since it's a record, we need to use init syntax
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingInMemoryStorage_IsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddIndexThinkingInMemoryStorage();

        // Act
        var provider = services.BuildServiceProvider();
        var store1 = provider.GetService<IThinkingStateStore>();
        var store2 = provider.GetService<IThinkingStateStore>();

        // Assert
        store1.Should().BeSameAs(store2);
    }

    [Fact]
    public void AddIndexThinkingInMemoryStorage_NullServices_Throws()
    {
        // Act
        var action = () => ((IServiceCollection)null!).AddIndexThinkingInMemoryStorage();

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIndexThinkingInMemoryStorage_NullOptions_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddIndexThinkingInMemoryStorage((InMemoryStateStoreOptions)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region SQLite Storage

    [Fact]
    public void AddIndexThinkingSqliteStorage_WithConnectionString_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIndexThinkingSqliteStorage("Data Source=:memory:");

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        store.Should().NotBeNull();
        store.Should().BeOfType<SqliteThinkingStateStore>();
    }

    [Fact]
    public void AddIndexThinkingSqliteStorage_WithOptions_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new SqliteStateStoreOptions
        {
            ConnectionString = "Data Source=:memory:",
            EnableWalMode = false
        };

        // Act
        services.AddIndexThinkingSqliteStorage(options);

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();
        var registeredOptions = provider.GetService<SqliteStateStoreOptions>();

        store.Should().NotBeNull();
        registeredOptions.Should().NotBeNull();
        registeredOptions!.EnableWalMode.Should().BeFalse();
    }

    [Fact]
    public void AddIndexThinkingSqliteStorage_WithConfigure_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIndexThinkingSqliteStorage(options =>
        {
            options.ConnectionString = "Data Source=:memory:";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        store.Should().NotBeNull();
        store.Should().BeOfType<SqliteThinkingStateStore>();
    }

    [Fact]
    public void AddIndexThinkingSqliteStorage_WithConfigure_EmptyConnectionString_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - should throw because connection string is not set
        var action = () => services.AddIndexThinkingSqliteStorage(o => { });

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionString*");
    }

    [Fact]
    public void AddIndexThinkingSqliteStorage_IsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddIndexThinkingSqliteStorage("Data Source=:memory:");

        // Act
        var provider = services.BuildServiceProvider();
        var store1 = provider.GetService<IThinkingStateStore>();
        var store2 = provider.GetService<IThinkingStateStore>();

        // Assert
        store1.Should().BeSameAs(store2);
    }

    [Fact]
    public void AddIndexThinkingSqliteStorage_NullServices_Throws()
    {
        // Act
        var action = () => ((IServiceCollection)null!).AddIndexThinkingSqliteStorage("Data Source=:memory:");

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIndexThinkingSqliteStorage_NullOptions_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddIndexThinkingSqliteStorage((SqliteStateStoreOptions)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Agent Services

    [Fact]
    public void AddIndexThinkingAgents_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Add required dependencies
        services.AddSingleton(Substitute.For<ITruncationDetector>());
        services.AddSingleton(Substitute.For<ITokenCounter>());
        services.AddSingleton<IEnumerable<IReasoningParser>>(Array.Empty<IReasoningParser>());

        // Act
        services.AddIndexThinkingAgents();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.GetService<IComplexityEstimator>().Should().NotBeNull();
        provider.GetService<IBudgetTracker>().Should().NotBeNull();
        provider.GetService<IContinuationHandler>().Should().NotBeNull();
        provider.GetService<AgentOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingAgents_WithOptions_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var customBudget = new BudgetConfig
        {
            ThinkingBudget = 5000,
            AnswerBudget = 2000
        };

        // Add required dependencies
        services.AddSingleton(Substitute.For<ITruncationDetector>());
        services.AddSingleton(Substitute.For<ITokenCounter>());
        services.AddSingleton<IEnumerable<IReasoningParser>>(Array.Empty<IReasoningParser>());

        // Act
        services.AddIndexThinkingAgents(options =>
        {
            options.DefaultBudget = customBudget;
            options.AutoEstimateComplexity = false;
        });
        var provider = services.BuildServiceProvider();
        var resolvedOptions = provider.GetRequiredService<AgentOptions>();

        // Assert
        resolvedOptions.DefaultBudget.ThinkingBudget.Should().Be(5000);
        resolvedOptions.DefaultBudget.AnswerBudget.Should().Be(2000);
        resolvedOptions.AutoEstimateComplexity.Should().BeFalse();
    }

    [Fact]
    public void Services_ResolveCorrectly_FromDI()
    {
        // Arrange
        var services = new ServiceCollection();

        // Add required dependencies
        services.AddSingleton(Substitute.For<ITruncationDetector>());
        services.AddSingleton(Substitute.For<ITokenCounter>());
        services.AddSingleton<IEnumerable<IReasoningParser>>(Array.Empty<IReasoningParser>());

        // Act
        services.AddIndexThinkingAgents();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IComplexityEstimator>().Should().BeOfType<HeuristicComplexityEstimator>();
        provider.GetService<IBudgetTracker>().Should().BeOfType<DefaultBudgetTracker>();
        provider.GetService<IContinuationHandler>().Should().BeOfType<DefaultContinuationHandler>();
    }

    [Fact]
    public void AddIndexThinkingAgents_NullServices_Throws()
    {
        // Act
        var action = () => ((IServiceCollection)null!).AddIndexThinkingAgents();

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIndexThinkingAgents_DefaultOptions_HasCorrectDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Add required dependencies
        services.AddSingleton(Substitute.For<ITruncationDetector>());
        services.AddSingleton(Substitute.For<ITokenCounter>());

        // Act
        services.AddIndexThinkingAgents();
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<AgentOptions>();

        // Assert
        options.AutoEstimateComplexity.Should().BeTrue();
        options.DefaultBudget.Should().NotBeNull();
        options.DefaultContinuation.Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingAgents_IsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Add required dependencies
        services.AddSingleton(Substitute.For<ITruncationDetector>());
        services.AddSingleton(Substitute.For<ITokenCounter>());
        services.AddSingleton<IEnumerable<IReasoningParser>>(Array.Empty<IReasoningParser>());

        services.AddIndexThinkingAgents();

        // Act
        var provider = services.BuildServiceProvider();
        var estimator1 = provider.GetService<IComplexityEstimator>();
        var estimator2 = provider.GetService<IComplexityEstimator>();

        // Assert
        estimator1.Should().BeSameAs(estimator2);
    }

    #endregion

    #region Multiple Registrations

    [Fact]
    public void MultipleRegistrations_FirstWins()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - register InMemory first, then SQLite
        services.AddIndexThinkingInMemoryStorage();
        services.AddIndexThinkingSqliteStorage("Data Source=:memory:");

        // Assert - first registration wins due to TryAdd
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        store.Should().BeOfType<InMemoryThinkingStateStore>();
    }

    [Fact]
    public void Chaining_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddIndexThinkingInMemoryStorage();

        // Assert
        result.Should().BeSameAs(services);
    }

    #endregion

    #region Distributed Cache Storage (v0.10.0)

    [Fact]
    public void AddIndexThinkingDistributedStorage_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();

        // Act
        services.AddIndexThinkingDistributedStorage();

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();

        store.Should().NotBeNull();
        store.Should().BeOfType<DistributedCacheThinkingStateStore>();
    }

    [Fact]
    public void AddIndexThinkingDistributedStorage_WithOptions_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        var options = new DistributedCacheStateStoreOptions
        {
            KeyPrefix = "custom:",
            AbsoluteExpiration = TimeSpan.FromHours(2)
        };

        // Act
        services.AddIndexThinkingDistributedStorage(options);

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();
        var registeredOptions = provider.GetService<DistributedCacheStateStoreOptions>();

        store.Should().NotBeNull();
        registeredOptions.Should().NotBeNull();
        registeredOptions!.KeyPrefix.Should().Be("custom:");
        registeredOptions.AbsoluteExpiration.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void AddIndexThinkingDistributedStorage_WithConfigure_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();

        // Act
        services.AddIndexThinkingDistributedStorage(options =>
        {
            options.KeyPrefix = "configured:";
            options.SlidingExpiration = TimeSpan.FromMinutes(30);
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IThinkingStateStore>();
        var registeredOptions = provider.GetService<DistributedCacheStateStoreOptions>();

        store.Should().NotBeNull();
        registeredOptions.Should().NotBeNull();
        registeredOptions!.KeyPrefix.Should().Be("configured:");
        registeredOptions.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void AddIndexThinkingDistributedStorage_IsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddIndexThinkingDistributedStorage();

        // Act
        var provider = services.BuildServiceProvider();
        var store1 = provider.GetService<IThinkingStateStore>();
        var store2 = provider.GetService<IThinkingStateStore>();

        // Assert
        store1.Should().BeSameAs(store2);
    }

    [Fact]
    public void AddIndexThinkingDistributedStorage_NullServices_Throws()
    {
        // Act
        var action = () => ((IServiceCollection)null!).AddIndexThinkingDistributedStorage();

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIndexThinkingDistributedStorage_NullOptions_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddIndexThinkingDistributedStorage((DistributedCacheStateStoreOptions)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIndexThinkingDistributedStorage_WithoutCache_ThrowsOnResolve()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddIndexThinkingDistributedStorage();

        // Act
        var provider = services.BuildServiceProvider();
        var action = () => provider.GetRequiredService<IThinkingStateStore>();

        // Assert - should throw because IDistributedCache is not registered
        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Health Checks (v0.10.0)

    [Fact]
    public void AddIndexThinkingHealthChecks_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IThinkingStateStore>());

        // Act
        services.AddIndexThinkingHealthChecks();

        // Assert
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var healthCheck = scope.ServiceProvider.GetService<ThinkingStateStoreHealthCheck>();

        healthCheck.Should().NotBeNull();
    }

    [Fact]
    public void AddIndexThinkingHealthChecks_WithOptions_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IThinkingStateStore>());
        var options = new ThinkingStateStoreHealthCheckOptions
        {
            Timeout = TimeSpan.FromSeconds(10),
            TestSessionId = "__custom__"
        };

        // Act
        services.AddIndexThinkingHealthChecks(options);

        // Assert
        var provider = services.BuildServiceProvider();
        var registeredOptions = provider.GetService<ThinkingStateStoreHealthCheckOptions>();

        registeredOptions.Should().NotBeNull();
        registeredOptions!.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        registeredOptions.TestSessionId.Should().Be("__custom__");
    }

    [Fact]
    public void AddIndexThinkingHealthChecks_WithConfigure_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IThinkingStateStore>());

        // Act
        services.AddIndexThinkingHealthChecks(options =>
        {
            options.Timeout = TimeSpan.FromSeconds(3);
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var registeredOptions = provider.GetService<ThinkingStateStoreHealthCheckOptions>();

        registeredOptions.Should().NotBeNull();
        registeredOptions!.Timeout.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void AddIndexThinkingHealthChecks_NullServices_Throws()
    {
        // Act
        var action = () => ((IServiceCollection)null!).AddIndexThinkingHealthChecks();

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIndexThinkingHealthChecks_NullOptions_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddIndexThinkingHealthChecks((ThinkingStateStoreHealthCheckOptions)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
