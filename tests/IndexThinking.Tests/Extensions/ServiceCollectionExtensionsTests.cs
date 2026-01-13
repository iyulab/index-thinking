using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using IndexThinking.Abstractions;
using IndexThinking.Extensions;
using IndexThinking.Stores;
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
}
