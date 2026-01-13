using System.Data;
using Microsoft.Data.Sqlite;
using IndexThinking.Abstractions;
using IndexThinking.Core;

namespace IndexThinking.Stores;

/// <summary>
/// SQLite-based implementation of <see cref="IThinkingStateStore"/> for persistent storage.
/// </summary>
/// <remarks>
/// <para>
/// Uses WAL (Write-Ahead Logging) mode for improved concurrency.
/// See: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async
/// </para>
/// <para>
/// Note: SQLite does not support true async I/O. Async methods execute synchronously
/// but maintain Task-based signatures for interface compatibility.
/// </para>
/// </remarks>
public sealed class SqliteThinkingStateStore : IThinkingStateStore, IDisposable
{
    private const string TableName = "thinking_states";

    private readonly SqliteStateStoreOptions _options;
    private readonly string _connectionString;
    private readonly bool _isInMemory;
    private readonly object _lock = new();
    private SqliteConnection? _persistentConnection;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Creates a new SQLite state store with the specified connection string.
    /// </summary>
    /// <param name="connectionString">SQLite connection string.</param>
    public SqliteThinkingStateStore(string connectionString)
        : this(new SqliteStateStoreOptions { ConnectionString = connectionString })
    {
    }

    /// <summary>
    /// Creates a new SQLite state store with the specified options.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    public SqliteThinkingStateStore(SqliteStateStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(options));
        }

        _connectionString = _options.ConnectionString;

        // Detect in-memory database
        _isInMemory = _connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
            || _connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<ThinkingState?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureInitialized();

        var connection = GetConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT
                    session_id, model_id,
                    reasoning_state_provider, reasoning_state_data, reasoning_state_captured_at,
                    total_thinking_tokens, total_output_tokens, continuation_count,
                    created_at, updated_at
                FROM {TableName}
                WHERE session_id = $sessionId";
            command.Parameters.AddWithValue("$sessionId", sessionId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return Task.FromResult<ThinkingState?>(null);
            }

            var state = ReadThinkingState(reader);
            return Task.FromResult<ThinkingState?>(state);
        }
        finally
        {
            ReleaseConnection(connection);
        }
    }

    /// <inheritdoc />
    public Task SetAsync(string sessionId, ThinkingState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(state);
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureInitialized();

        var connection = GetConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                INSERT OR REPLACE INTO {TableName} (
                    session_id, model_id,
                    reasoning_state_provider, reasoning_state_data, reasoning_state_captured_at,
                    total_thinking_tokens, total_output_tokens, continuation_count,
                    created_at, updated_at
                ) VALUES (
                    $sessionId, $modelId,
                    $rsProvider, $rsData, $rsCapturedAt,
                    $thinkingTokens, $outputTokens, $continuationCount,
                    $createdAt, $updatedAt
                )";

            command.Parameters.AddWithValue("$sessionId", sessionId);
            command.Parameters.AddWithValue("$modelId", (object?)state.ModelId ?? DBNull.Value);

            if (state.ReasoningState is not null)
            {
                command.Parameters.AddWithValue("$rsProvider", state.ReasoningState.Provider);
                command.Parameters.AddWithValue("$rsData", state.ReasoningState.Data);
                command.Parameters.AddWithValue("$rsCapturedAt", state.ReasoningState.CapturedAt.ToString("O"));
            }
            else
            {
                command.Parameters.AddWithValue("$rsProvider", DBNull.Value);
                command.Parameters.AddWithValue("$rsData", DBNull.Value);
                command.Parameters.AddWithValue("$rsCapturedAt", DBNull.Value);
            }

            command.Parameters.AddWithValue("$thinkingTokens", state.TotalThinkingTokens);
            command.Parameters.AddWithValue("$outputTokens", state.TotalOutputTokens);
            command.Parameters.AddWithValue("$continuationCount", state.ContinuationCount);
            command.Parameters.AddWithValue("$createdAt", state.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$updatedAt", state.UpdatedAt.ToString("O"));

            command.ExecuteNonQuery();

            return Task.CompletedTask;
        }
        finally
        {
            ReleaseConnection(connection);
        }
    }

    /// <inheritdoc />
    public Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureInitialized();

        var connection = GetConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {TableName} WHERE session_id = $sessionId";
            command.Parameters.AddWithValue("$sessionId", sessionId);
            command.ExecuteNonQuery();

            return Task.CompletedTask;
        }
        finally
        {
            ReleaseConnection(connection);
        }
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureInitialized();

        var connection = GetConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT 1 FROM {TableName} WHERE session_id = $sessionId LIMIT 1";
            command.Parameters.AddWithValue("$sessionId", sessionId);

            var result = command.ExecuteScalar();
            return Task.FromResult(result is not null);
        }
        finally
        {
            ReleaseConnection(connection);
        }
    }

    /// <summary>
    /// Gets the count of stored states.
    /// </summary>
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureInitialized();

            var connection = GetConnection();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM {TableName}";

                var result = command.ExecuteScalar();
                return Convert.ToInt32(result);
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }
    }

    /// <summary>
    /// Clears all stored states.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureInitialized();

        var connection = GetConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {TableName}";
            command.ExecuteNonQuery();
        }
        finally
        {
            ReleaseConnection(connection);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose the persistent connection for in-memory databases
        _persistentConnection?.Dispose();
        _persistentConnection = null;
    }

    private SqliteConnection GetConnection()
    {
        if (_isInMemory)
        {
            // For in-memory databases, return the persistent connection
            // (already opened in EnsureInitialized)
            return _persistentConnection!;
        }

        // For file-based databases, create a new connection
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void ReleaseConnection(SqliteConnection connection)
    {
        // Only dispose connections for file-based databases
        // In-memory connections must stay alive
        if (!_isInMemory)
        {
            connection.Dispose();
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            SqliteConnection connection;
            if (_isInMemory)
            {
                // For in-memory databases, create a persistent connection that stays alive
                _persistentConnection = new SqliteConnection(_connectionString);
                _persistentConnection.Open();
                connection = _persistentConnection;
            }
            else
            {
                connection = new SqliteConnection(_connectionString);
                connection.Open();
            }

            // Enable WAL mode for better concurrency
            if (_options.EnableWalMode)
            {
                using var walCommand = connection.CreateCommand();
                walCommand.CommandText = "PRAGMA journal_mode = 'wal'";
                walCommand.ExecuteNonQuery();
            }

            // Create table if not exists
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {TableName} (
                    session_id TEXT PRIMARY KEY,
                    model_id TEXT,
                    reasoning_state_provider TEXT,
                    reasoning_state_data BLOB,
                    reasoning_state_captured_at TEXT,
                    total_thinking_tokens INTEGER NOT NULL DEFAULT 0,
                    total_output_tokens INTEGER NOT NULL DEFAULT 0,
                    continuation_count INTEGER NOT NULL DEFAULT 0,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                )";
            createCommand.ExecuteNonQuery();

            // Create index on updated_at for potential cleanup queries
            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = $@"
                CREATE INDEX IF NOT EXISTS idx_{TableName}_updated_at
                ON {TableName}(updated_at)";
            indexCommand.ExecuteNonQuery();

            // For file-based databases, dispose the initialization connection
            if (!_isInMemory)
            {
                connection.Dispose();
            }

            _initialized = true;
        }
    }

    private static ThinkingState ReadThinkingState(SqliteDataReader reader)
    {
        ReasoningState? reasoningState = null;

        var rsProvider = reader.IsDBNull("reasoning_state_provider")
            ? null
            : reader.GetString("reasoning_state_provider");

        if (rsProvider is not null)
        {
            var rsData = (byte[])reader["reasoning_state_data"];
            var rsCapturedAt = DateTimeOffset.Parse(reader.GetString("reasoning_state_captured_at"));

            reasoningState = new ReasoningState
            {
                Provider = rsProvider,
                Data = rsData,
                CapturedAt = rsCapturedAt
            };
        }

        return new ThinkingState
        {
            SessionId = reader.GetString("session_id"),
            ModelId = reader.IsDBNull("model_id") ? null : reader.GetString("model_id"),
            ReasoningState = reasoningState,
            TotalThinkingTokens = reader.GetInt32("total_thinking_tokens"),
            TotalOutputTokens = reader.GetInt32("total_output_tokens"),
            ContinuationCount = reader.GetInt32("continuation_count"),
            CreatedAt = DateTimeOffset.Parse(reader.GetString("created_at")),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString("updated_at"))
        };
    }
}

/// <summary>
/// Configuration options for <see cref="SqliteThinkingStateStore"/>.
/// </summary>
public sealed class SqliteStateStoreOptions
{
    /// <summary>
    /// SQLite connection string.
    /// Example: "Data Source=thinking_states.db"
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Whether to enable WAL (Write-Ahead Logging) mode for better concurrency.
    /// Default: true.
    /// </summary>
    public bool EnableWalMode { get; set; } = true;

    /// <summary>
    /// Creates options for an in-memory SQLite database (useful for testing).
    /// </summary>
    public static SqliteStateStoreOptions InMemory => new()
    {
        ConnectionString = "Data Source=:memory:;Mode=Memory;Cache=Shared"
    };

    /// <summary>
    /// Creates options for a file-based SQLite database.
    /// </summary>
    /// <param name="filePath">Path to the database file.</param>
    /// <returns>Configured options.</returns>
    public static SqliteStateStoreOptions FromFile(string filePath) => new()
    {
        ConnectionString = $"Data Source={filePath}"
    };
}
