namespace SmartSchoolTimetable.Application;

public interface ILocalSessionStore
{
    string Issue(string username);
    bool TryValidateAndTouch(string sessionId, TimeSpan inactivityTimeout, out string username);
    void Revoke(string sessionId);
    void RevokeAll();
}
