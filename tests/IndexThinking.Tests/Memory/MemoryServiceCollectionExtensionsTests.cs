using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Extensions;
using IndexThinking.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IndexThinking.Tests.Memory;

public class MemoryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIndexThinkingNullMemory_RegistersNullProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIndexThinkingNullMemory();
        var provider = services.BuildServiceProvider();
        var memoryProvider = provider.GetRequiredService<IMemoryProvider>();

        // Assert
        memoryProvider.Should().BeSameAs(NullMemoryProvider.Instance);
        memoryProvider.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void AddIndexThinkingNullMemory_NullServices_Throws()
    {
        // Act
        var action = () => ServiceCollectionExtensions.AddIndexThinkingNullMemory(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIndexThinkingMemory_WithDelegate_RegistersFuncProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIndexThinkingMemory((userId, sessionId, query, limit, ct) =>
        {
            return Task.FromResult(MemoryRecallResult.Empty);
        });

        var provider = services.BuildServiceProvider();
        var memoryProvider = provider.GetRequiredService<IMemoryProvider>();

        // Assert
        memoryProvider.Should().BeOfType<FuncMemoryProvider>();
        memoryProvider.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void AddIndexThinkingMemory_NullDelegate_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddIndexThinkingMemory((MemoryRecallDelegate)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIndexThinkingMemory_WithInstance_RegistersInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var customProvider = new FuncMemoryProvider((_, _, _, _, _) =>
            Task.FromResult(MemoryRecallResult.Empty));

        // Act
        services.AddIndexThinkingMemory(customProvider);
        var provider = services.BuildServiceProvider();
        var memoryProvider = provider.GetRequiredService<IMemoryProvider>();

        // Assert
        memoryProvider.Should().BeSameAs(customProvider);
    }

    [Fact]
    public void AddIndexThinkingMemory_WithGeneric_RegistersType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIndexThinkingMemory<TestMemoryProvider>();
        var provider = services.BuildServiceProvider();
        var memoryProvider = provider.GetRequiredService<IMemoryProvider>();

        // Assert
        memoryProvider.Should().BeOfType<TestMemoryProvider>();
    }

    [Fact]
    public void AddIndexThinkingMemory_NullInstance_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var action = () => services.AddIndexThinkingMemory((IMemoryProvider)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    private sealed class TestMemoryProvider : IMemoryProvider
    {
        public bool IsConfigured => true;

        public Task<MemoryRecallContext> RecallAsync(
            string userId,
            string? sessionId,
            string query,
            int limit = 10,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MemoryRecallContext.Empty(query));
        }

        public Task RememberAsync(
            string userId,
            string? sessionId,
            IEnumerable<MemoryStoreRequest> memories,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
