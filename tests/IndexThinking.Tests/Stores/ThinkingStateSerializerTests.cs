using FluentAssertions;
using IndexThinking.Core;
using IndexThinking.Stores;
using Xunit;

namespace IndexThinking.Tests.Stores;

public class ThinkingStateSerializerTests
{
    [Fact]
    public void Serialize_SimpleState_ReturnsValidJson()
    {
        // Arrange
        var state = new ThinkingState
        {
            SessionId = "session-1",
            TotalThinkingTokens = 100
        };

        // Act
        var json = ThinkingStateSerializer.SerializeToString(state);

        // Assert
        json.Should().Contain("\"session_id\"");
        json.Should().Contain("\"session-1\"");
        json.Should().Contain("\"total_thinking_tokens\"");
        json.Should().Contain("100");
    }

    [Fact]
    public void Serialize_WithReasoningState_IncludesBase64Data()
    {
        // Arrange
        var state = new ThinkingState
        {
            SessionId = "session-1",
            ReasoningState = new ReasoningState
            {
                Provider = "openai",
                Data = [1, 2, 3, 4, 5]
            }
        };

        // Act
        var json = ThinkingStateSerializer.SerializeToString(state);

        // Assert
        json.Should().Contain("\"reasoning_state\"");
        json.Should().Contain("\"provider\"");
        json.Should().Contain("\"openai\"");
        json.Should().Contain("\"data\"");
        // Base64 of [1,2,3,4,5] is "AQIDBAU="
        json.Should().Contain("AQIDBAU=");
    }

    [Fact]
    public void SerializeToBytes_ReturnsUtf8Bytes()
    {
        // Arrange
        var state = new ThinkingState { SessionId = "session-1" };

        // Act
        var bytes = ThinkingStateSerializer.Serialize(state);

        // Assert
        bytes.Should().NotBeEmpty();
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        json.Should().Contain("session_id");
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsState()
    {
        // Arrange
        var json = """{"session_id":"session-1","total_thinking_tokens":100}""";

        // Act
        var state = ThinkingStateSerializer.Deserialize(json);

        // Assert
        state.Should().NotBeNull();
        state!.SessionId.Should().Be("session-1");
        state.TotalThinkingTokens.Should().Be(100);
    }

    [Fact]
    public void Deserialize_WithReasoningState_DecodesBase64Data()
    {
        // Arrange
        var json = """
        {
            "session_id": "session-1",
            "reasoning_state": {
                "provider": "openai",
                "data": "AQIDBAU=",
                "captured_at": "2024-01-15T10:30:00+00:00"
            }
        }
        """;

        // Act
        var state = ThinkingStateSerializer.Deserialize(json);

        // Assert
        state.Should().NotBeNull();
        state!.ReasoningState.Should().NotBeNull();
        state.ReasoningState!.Provider.Should().Be("openai");
        state.ReasoningState.Data.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void Deserialize_FromBytes_ReturnsState()
    {
        // Arrange
        var json = """{"session_id":"session-1"}""";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        // Act
        var state = ThinkingStateSerializer.Deserialize(bytes);

        // Assert
        state.Should().NotBeNull();
        state!.SessionId.Should().Be("session-1");
    }

    [Fact]
    public void Deserialize_NullOrEmpty_ReturnsNull()
    {
        // Act & Assert
        ThinkingStateSerializer.Deserialize((string?)null).Should().BeNull();
        ThinkingStateSerializer.Deserialize("").Should().BeNull();
        ThinkingStateSerializer.Deserialize((byte[]?)null).Should().BeNull();
        ThinkingStateSerializer.Deserialize(Array.Empty<byte>()).Should().BeNull();
    }

    [Fact]
    public void RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new ThinkingState
        {
            SessionId = "session-roundtrip",
            ModelId = "gpt-4o",
            ReasoningState = new ReasoningState
            {
                Provider = "anthropic",
                Data = [10, 20, 30, 40, 50],
                CapturedAt = new DateTimeOffset(2024, 6, 15, 14, 30, 0, TimeSpan.Zero)
            },
            TotalThinkingTokens = 1000,
            TotalOutputTokens = 500,
            ContinuationCount = 3,
            CreatedAt = new DateTimeOffset(2024, 6, 15, 14, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2024, 6, 15, 14, 30, 0, TimeSpan.Zero)
        };

        // Act
        var bytes = ThinkingStateSerializer.Serialize(original);
        var restored = ThinkingStateSerializer.Deserialize(bytes);

        // Assert
        restored.Should().NotBeNull();
        restored!.SessionId.Should().Be(original.SessionId);
        restored.ModelId.Should().Be(original.ModelId);
        restored.TotalThinkingTokens.Should().Be(original.TotalThinkingTokens);
        restored.TotalOutputTokens.Should().Be(original.TotalOutputTokens);
        restored.ContinuationCount.Should().Be(original.ContinuationCount);
        restored.CreatedAt.Should().Be(original.CreatedAt);
        restored.UpdatedAt.Should().Be(original.UpdatedAt);

        restored.ReasoningState.Should().NotBeNull();
        restored.ReasoningState!.Provider.Should().Be(original.ReasoningState.Provider);
        restored.ReasoningState.Data.Should().BeEquivalentTo(original.ReasoningState.Data);
        restored.ReasoningState.CapturedAt.Should().Be(original.ReasoningState.CapturedAt);
    }

    [Fact]
    public void Serialize_NullState_ThrowsArgumentNullException()
    {
        // Act
        var action = () => ThinkingStateSerializer.Serialize(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SerializeToString_NullState_ThrowsArgumentNullException()
    {
        // Act
        var action = () => ThinkingStateSerializer.SerializeToString(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetDefaultOptions_ReturnsNonNullOptions()
    {
        // Act
        var options = ThinkingStateSerializer.GetDefaultOptions();

        // Assert
        options.Should().NotBeNull();
    }

    [Fact]
    public void Serialize_UsesSnakeCaseNaming()
    {
        // Arrange
        var state = new ThinkingState
        {
            SessionId = "session-1",
            TotalThinkingTokens = 100,
            TotalOutputTokens = 50,
            ContinuationCount = 2
        };

        // Act
        var json = ThinkingStateSerializer.SerializeToString(state);

        // Assert
        json.Should().Contain("session_id");
        json.Should().Contain("total_thinking_tokens");
        json.Should().Contain("total_output_tokens");
        json.Should().Contain("continuation_count");
        json.Should().NotContain("SessionId");
        json.Should().NotContain("TotalThinkingTokens");
    }

    [Fact]
    public void Serialize_OmitsNullValues()
    {
        // Arrange
        var state = new ThinkingState
        {
            SessionId = "session-1",
            ModelId = null,
            ReasoningState = null
        };

        // Act
        var json = ThinkingStateSerializer.SerializeToString(state);

        // Assert
        json.Should().NotContain("model_id");
        json.Should().NotContain("reasoning_state");
    }
}
