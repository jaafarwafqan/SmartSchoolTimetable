using System.Collections.Concurrent;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Infrastructure;

public sealed class LocalSessionStore(TimeProvider timeProvider) : ILocalSessionStore
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    public string Issue(string username, bool recoveryCodeIssued = false)
    {
        var sessionId = SecureToken.CreateUrlSafe();
        var now = timeProvider.GetUtcNow();
        _sessions[sessionId] = new Session(username, now, recoveryCodeIssued);
        return sessionId;
    }

    public bool TryValidateAndTouch(string sessionId, TimeSpan? inactivityTimeout, out string username)
    {
        username = string.Empty;
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        var now = timeProvider.GetUtcNow();
        if (inactivityTimeout is { } timeout && now - session.LastActivity >= timeout)
        {
            _sessions.TryRemove(sessionId, out _);
            return false;
        }

        session.LastActivity = now;
        username = session.Username;
        return true;
    }

    public void Revoke(string sessionId) => _sessions.TryRemove(sessionId, out _);

    public void RevokeAll() => _sessions.Clear();

    public void RenameAll(string username)
    {
        foreach (var session in _sessions.Values)
            session.Username = username;
    }

    public void MarkRecoveryCodeIssued(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            session.RecoveryCodeIssued = true;
    }

    public bool HasRecoveryCodeIssued(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) && session.RecoveryCodeIssued;

    private sealed class Session(string username, DateTimeOffset lastActivity, bool recoveryCodeIssued)
    {
        public string Username { get; set; } = username;
        public DateTimeOffset LastActivity { get; set; } = lastActivity;
        public bool RecoveryCodeIssued { get; set; } = recoveryCodeIssued;
    }
}
