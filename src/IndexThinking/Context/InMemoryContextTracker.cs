using System.Collections.Concurrent;
using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;

namespace IndexThinking.Context;

/// <summary>
/// In-memory implementation of <see cref="IContextTracker"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses a sliding window approach to maintain recent conversation history.
/// Older turns are automatically evicted when the configured limit is reached.
/// </para>
/// <para>
/// This implementation is thread-safe and suitable for single-server deployments.
/// For distributed scenarios, consider implementing a custom tracker backed by
/// Redis or a similar distributed cache.
/// </para>
/// </remarks>
public sealed class InMemoryContextTracker : IContextTracker, IDisposable
{
    private readonly ConcurrentDictionary<string, SessionContext> _sessions = new();
    private readonly ContextTrackerOptions _options;
    private readonly Timer? _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new tracker with default options.
    /// </summary>
    public InMemoryContextTracker() : this(ContextTrackerOptions.Default) { }

    /// <summary>
    /// Creates a new tracker with the specified options.
    /// </summary>
    public InMemoryContextTracker(ContextTrackerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (options.EnableCleanupTimer)
        {
            _cleanupTimer = new Timer(
                CleanupExpiredSessions,
                null,
                options.CleanupInterval,
                options.CleanupInterval);
        }
    }

    /// <inheritdoc />
    public void Track(string sessionId, ChatMessage userMessage, ChatResponse? response = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(userMessage);

        var turn = new ConversationTurn
        {
            UserMessage = userMessage,
            AssistantResponse = response
        };

        _sessions.AddOrUpdate(
            sessionId,
            _ => CreateNewSession(sessionId, turn),
            (_, existing) => existing.AddTurn(turn, _options.MaxTurns));
    }

    /// <inheritdoc />
    public ConversationContext GetContext(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            // Check if session is expired
            if (IsExpired(session))
            {
                _sessions.TryRemove(sessionId, out _);
                return ConversationContext.Empty(sessionId);
            }

            return session.ToContext();
        }

        return ConversationContext.Empty(sessionId);
    }

    /// <inheritdoc />
    public void Clear(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _sessions.TryRemove(sessionId, out _);
    }

    /// <inheritdoc />
    public bool HasContext(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            if (IsExpired(session))
            {
                _sessions.TryRemove(sessionId, out _);
                return false;
            }
            return session.TurnCount > 0;
        }

        return false;
    }

    /// <summary>
    /// Gets the number of active sessions.
    /// </summary>
    public int SessionCount => _sessions.Count;

    private SessionContext CreateNewSession(string sessionId, ConversationTurn turn)
    {
        return new SessionContext(sessionId, turn);
    }

    private bool IsExpired(SessionContext session)
    {
        return DateTimeOffset.UtcNow - session.LastActivityAt > _options.SessionTtl;
    }

    private void CleanupExpiredSessions(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _sessions
            .Where(kvp => now - kvp.Value.LastActivityAt > _options.SessionTtl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _sessions.TryRemove(key, out _);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer?.Dispose();
    }

    /// <summary>
    /// Internal session context with thread-safe turn management.
    /// </summary>
    private sealed class SessionContext
    {
        private readonly object _lock = new();
        private readonly List<ConversationTurn> _turns = new();
        private readonly string _sessionId;
        private readonly DateTimeOffset _createdAt;

        public SessionContext(string sessionId, ConversationTurn initialTurn)
        {
            _sessionId = sessionId;
            _createdAt = DateTimeOffset.UtcNow;
            _turns.Add(initialTurn);
            TotalTurnCount = 1;
            LastActivityAt = initialTurn.Timestamp;
        }

        public int TurnCount
        {
            get { lock (_lock) return _turns.Count; }
        }

        public int TotalTurnCount { get; private set; }

        public DateTimeOffset LastActivityAt { get; private set; }

        public SessionContext AddTurn(ConversationTurn turn, int maxTurns)
        {
            lock (_lock)
            {
                _turns.Add(turn);
                TotalTurnCount++;
                LastActivityAt = turn.Timestamp;

                // Evict oldest turns if over limit
                while (_turns.Count > maxTurns)
                {
                    _turns.RemoveAt(0);
                }
            }
            return this;
        }

        public ConversationContext ToContext()
        {
            lock (_lock)
            {
                return new ConversationContext
                {
                    SessionId = _sessionId,
                    RecentTurns = _turns.ToList().AsReadOnly(),
                    TotalTurnCount = TotalTurnCount,
                    SessionStartedAt = _createdAt,
                    LastTurnAt = _turns.Count > 0 ? _turns[^1].Timestamp : null
                };
            }
        }
    }
}
