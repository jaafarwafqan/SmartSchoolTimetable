using System.Collections.Concurrent;
using System.Security.Cryptography;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Infrastructure;

public sealed class LocalSessionStore(TimeProvider timeProvider) : ILocalSessionStore
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    public string Issue(string username)
    {
        var sessionId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var now = timeProvider.GetUtcNow();
        _sessions[sessionId] = new Session(username, now);
        return sessionId;
    }

    public bool TryValidateAndTouch(string sessionId, TimeSpan inactivityTimeout, out string username)
    {
        username = string.Empty;
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        var now = timeProvider.GetUtcNow();
        if (now - session.LastActivity >= inactivityTimeout)
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

    private sealed class Session(string username, DateTimeOffset lastActivity)
    {
        public string Username { get; } = username;
        public DateTimeOffset LastActivity { get; set; } = lastActivity;
    }
}
