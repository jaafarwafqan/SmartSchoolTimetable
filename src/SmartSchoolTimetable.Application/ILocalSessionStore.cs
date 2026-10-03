namespace SmartSchoolTimetable.Application;

public interface ILocalSessionStore
{
    string Issue(string username, bool recoveryCodeIssued = false);
    bool TryValidateAndTouch(string sessionId, TimeSpan? inactivityTimeout, out string username);
    void Revoke(string sessionId);
    void RevokeAll();
    void MarkRecoveryCodeIssued(string sessionId);
    bool HasRecoveryCodeIssued(string sessionId);
}
