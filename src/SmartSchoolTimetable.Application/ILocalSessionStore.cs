namespace SmartSchoolTimetable.Application;

public interface ILocalSessionStore
{
    string Issue(string username, bool recoveryCodeIssued = false);
    bool TryValidateAndTouch(string sessionId, TimeSpan? inactivityTimeout, out string username);
    void Revoke(string sessionId);
    void RevokeAll();

    /// <summary>The username shown for every live session (there is one owner) after it was changed.</summary>
    void RenameAll(string username);
    void MarkRecoveryCodeIssued(string sessionId);
    bool HasRecoveryCodeIssued(string sessionId);
}
