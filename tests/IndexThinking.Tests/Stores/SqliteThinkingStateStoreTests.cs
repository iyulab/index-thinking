using FluentAssertions;
using IndexThinking.Core;
using IndexThinking.Stores;
using Microsoft.Data.Sqlite;
using Xunit;

namespace IndexThinking.Tests.Stores;

/// <summary>
/// Tests for <see cref="SqliteThinkingStateStore"/> inheriting common tests.
/// Uses in-memory SQLite for fast testing.
/// </summary>
public class SqliteThinkingStateStoreTests : ThinkingStateStoreTestsBase<SqliteThinkingStateStore>
{
    // Use a unique connection string with shared cache for in-memory database
    private static int _connectionCounter;
    private readonly string _connectionString;

    public SqliteThinkingStateStoreTests()
    {
        var id = Interlocked.Increment(ref _connectionCounter);
        _connectionString = $"Data Source=InMemory{id};Mode=Memory;Cache=Shared";
    }

    protected override SqliteThinkingStateStore CreateStore()
    {
        return new SqliteThinkingStateStore(_connectionString);
    }

    #region SQLite-Specific Tests

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
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        // Act
        var action = () => new SqliteThinkingStateStore((SqliteStateStoreOptions)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyConnectionString_ShouldThrow()
    {
        // Act
        var action = () => new SqliteThinkingStateStore("");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Timestamps_ShouldBePreserved()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var updatedAt = new DateTimeOffset(2024, 1, 15, 11, 45, 30, TimeSpan.Zero);

        var state = new ThinkingState
        {
            SessionId = "session-timestamps",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        // Act
        await Store.SetAsync("session-timestamps", state);
        var result = await Store.GetAsync("session-timestamps");

        // Assert
        result.Should().NotBeNull();
        result!.CreatedAt.Should().Be(createdAt);
        result.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public async Task NullModelId_ShouldBePreserved()
    {
        // Arrange
        var state = new ThinkingState
        {
            SessionId = "session-null-model",
            ModelId = null
        };

        // Act
        await Store.SetAsync("session-null-model", state);
        var result = await Store.GetAsync("session-null-model");

        // Assert
        result.Should().NotBeNull();
        result!.ModelId.Should().BeNull();
    }

    [Fact]
    public async Task NullReasoningState_ShouldBePreserved()
    {
        // Arrange
        var state = new ThinkingState
        {
            SessionId = "session-null-reasoning",
            ReasoningState = null
        };

        // Act
        await Store.SetAsync("session-null-reasoning", state);
        var result = await Store.GetAsync("session-null-reasoning");

        // Assert
        result.Should().NotBeNull();
        result!.ReasoningState.Should().BeNull();
    }

    [Fact]
    public async Task LargeReasoningStateData_ShouldBePreserved()
    {
        // Arrange
        var largeData = new byte[10000];
        new Random(42).NextBytes(largeData);

        var state = new ThinkingState
        {
            SessionId = "session-large-data",
            ReasoningState = new ReasoningState
            {
                Provider = "test",
                Data = largeData
            }
        };

        // Act
        await Store.SetAsync("session-large-data", state);
        var result = await Store.GetAsync("session-large-data");

        // Assert
        result.Should().NotBeNull();
        result!.ReasoningState.Should().NotBeNull();
        result.ReasoningState!.Data.Should().BeEquivalentTo(largeData);
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="SqliteStateStoreOptions"/>.
/// </summary>
public class SqliteStateStoreOptionsTests
{
    [Fact]
    public void InMemory_ShouldHaveCorrectConnectionString()
    {
        // Act
        var options = SqliteStateStoreOptions.InMemory;

        // Assert
        options.ConnectionString.Should().Contain("Mode=Memory");
        options.ConnectionString.Should().Contain("Cache=Shared");
        options.EnableWalMode.Should().BeTrue();
    }

    [Fact]
    public void FromFile_ShouldCreateCorrectConnectionString()
    {
        // Act
        var options = SqliteStateStoreOptions.FromFile("test.db");

        // Assert
        options.ConnectionString.Should().Be("Data Source=test.db");
        options.EnableWalMode.Should().BeTrue();
    }

    [Fact]
    public void Options_SupportsInitSyntax()
    {
        // Act
        var options = new SqliteStateStoreOptions
        {
            ConnectionString = "Data Source=custom.db",
            EnableWalMode = false
        };

        // Assert
        options.ConnectionString.Should().Be("Data Source=custom.db");
        options.EnableWalMode.Should().BeFalse();
    }

    [Fact]
    public void EnableWalMode_DefaultsToTrue()
    {
        // Act
        var options = new SqliteStateStoreOptions
        {
            ConnectionString = "Data Source=test.db"
        };

        // Assert
        options.EnableWalMode.Should().BeTrue();
    }
}

/// <summary>
/// Tests for file-based SQLite storage.
/// </summary>
public class SqliteThinkingStateStoreFileTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteThinkingStateStore _store;

    public SqliteThinkingStateStoreFileTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _store = new SqliteThinkingStateStore($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        _store.Dispose();
        GC.SuppressFinalize(this);

        // Clear the connection pool to release file locks
        SqliteConnection.ClearAllPools();

        // Clean up database files
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
            if (File.Exists($"{_dbPath}-wal"))
            {
                File.Delete($"{_dbPath}-wal");
            }
            if (File.Exists($"{_dbPath}-shm"))
            {
                File.Delete($"{_dbPath}-shm");
            }
        }
        catch (IOException)
        {
            // Ignore file cleanup failures in test
        }
    }

    [Fact]
    public async Task DatabaseFile_ShouldBeCreated()
    {
        // Act - trigger initialization
        await _store.SetAsync("session-1", new ThinkingState { SessionId = "session-1" });

        // Assert
        File.Exists(_dbPath).Should().BeTrue();
    }

    [Fact]
    public async Task Data_ShouldPersistAcrossInstances()
    {
        // Arrange
        await _store.SetAsync("session-1", new ThinkingState
        {
            SessionId = "session-1",
            TotalThinkingTokens = 999
        });

        // Act - create new instance with same database
        using var newStore = new SqliteThinkingStateStore($"Data Source={_dbPath}");
        var result = await newStore.GetAsync("session-1");

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be("session-1");
        result.TotalThinkingTokens.Should().Be(999);
    }
}

/// <summary>
/// Tests for disposed <see cref="SqliteThinkingStateStore"/>.
/// </summary>
public class SqliteThinkingStateStoreDisposeTests
{
    [Fact]
    public async Task GetAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var store = new SqliteThinkingStateStore("Data Source=:memory:");
        store.Dispose();

        // Act
        var action = () => store.GetAsync("session-1");

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var store = new SqliteThinkingStateStore("Data Source=:memory:");

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
