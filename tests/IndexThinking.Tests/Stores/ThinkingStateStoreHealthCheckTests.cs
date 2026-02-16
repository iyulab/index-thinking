using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using IndexThinking.Abstractions;
using IndexThinking.Stores;
using Xunit;

namespace IndexThinking.Tests.Stores;

/// <summary>
/// Tests for <see cref="ThinkingStateStoreHealthCheck"/>.
/// </summary>
public class ThinkingStateStoreHealthCheckTests
{
    [Fact]
    public void Constructor_WithNullStore_ShouldThrow()
    {
        // Act
        var action = () => new ThinkingStateStoreHealthCheck(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("store");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStoreIsResponsive_ShouldReturnHealthy()
    {
        // Arrange
        var mockStore = Substitute.For<IThinkingStateStore>();
        mockStore
            .ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var healthCheck = new ThinkingStateStoreHealthCheck(mockStore);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("responsive");
        result.Data.Should().ContainKey("store_type");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStoreThrows_ShouldReturnUnhealthy()
    {
        // Arrange
        var mockStore = Substitute.For<IThinkingStateStore>();
        mockStore
            .ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<bool>(x => throw new InvalidOperationException("Connection failed"));

        var healthCheck = new ThinkingStateStoreHealthCheck(mockStore);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeOfType<InvalidOperationException>();
        result.Data.Should().ContainKey("error_type");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTimesOut_ShouldReturnDegraded()
    {
        // Arrange
        var mockStore = Substitute.For<IThinkingStateStore>();
        mockStore
            .ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(1);
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return false;
            });

        var options = new ThinkingStateStoreHealthCheckOptions
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        var healthCheck = new ThinkingStateStoreHealthCheck(mockStore, options);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("timed out");
    }

    [Fact]
    public async Task CheckHealthAsync_UsesConfiguredTestSessionId()
    {
        // Arrange
        var capturedSessionId = string.Empty;
        var mockStore = Substitute.For<IThinkingStateStore>();
        mockStore
            .ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedSessionId = callInfo.ArgAt<string>(0);
                return Task.FromResult(false);
            });

        var options = new ThinkingStateStoreHealthCheckOptions
        {
            TestSessionId = "__custom_test__"
        };

        var healthCheck = new ThinkingStateStoreHealthCheck(mockStore, options);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        await healthCheck.CheckHealthAsync(context);

        // Assert
        capturedSessionId.Should().Be("__custom_test__");
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesSessionExistsInData()
    {
        // Arrange
        var mockStore = Substitute.For<IThinkingStateStore>();
        mockStore
            .ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var healthCheck = new ThinkingStateStoreHealthCheck(mockStore);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("session_exists");
        result.Data["session_exists"].Should().Be(true);
    }
}

/// <summary>
/// Tests for <see cref="ThinkingStateStoreHealthCheckOptions"/>.
/// </summary>
public class ThinkingStateStoreHealthCheckOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeReasonable()
    {
        // Act
        var options = new ThinkingStateStoreHealthCheckOptions();

        // Assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        options.TestSessionId.Should().Be("__health_check__");
    }
}
