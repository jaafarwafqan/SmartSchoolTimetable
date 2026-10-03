namespace SmartSchoolTimetable.Application;

public interface ICredentialHasher
{
    int CurrentPasswordIterations { get; }
    (byte[] Salt, byte[] Hash) HashPassword(string password);
    bool VerifyPassword(string password, byte[] salt, byte[] expectedHash, int iterations);
    (byte[] Salt, byte[] Hash) HashRecoveryCode(string recoveryCode);
    bool VerifyRecoveryCode(string recoveryCode, byte[] salt, byte[] expectedHash);
}
