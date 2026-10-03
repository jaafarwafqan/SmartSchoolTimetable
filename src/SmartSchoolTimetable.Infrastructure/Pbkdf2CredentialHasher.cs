using System.Security.Cryptography;
using System.Text;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Infrastructure;

public sealed class Pbkdf2CredentialHasher : ICredentialHasher
{
    public const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public int CurrentPasswordIterations => Iterations;

    public (byte[] Salt, byte[] Hash) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return (salt, hash);
    }

    public bool VerifyPassword(string password, byte[] salt, byte[] expectedHash, int iterations)
    {
        if (iterations < Iterations || salt.Length != SaltSize || expectedHash.Length != HashSize)
            return false;
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public (byte[] Salt, byte[] Hash) HashRecoveryCode(string recoveryCode)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return (salt, HashRecoveryCodeValue(recoveryCode, salt));
    }

    public bool VerifyRecoveryCode(string recoveryCode, byte[] salt, byte[] expectedHash)
    {
        if (salt.Length != SaltSize || expectedHash.Length != HashSize)
            return false;
        var actualHash = HashRecoveryCodeValue(recoveryCode, salt);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] HashRecoveryCodeValue(string recoveryCode, byte[] salt)
    {
        var input = new byte[salt.Length + Encoding.UTF8.GetByteCount(recoveryCode)];
        salt.CopyTo(input, 0);
        Encoding.UTF8.GetBytes(recoveryCode, input.AsSpan(salt.Length));
        return SHA256.HashData(input);
    }
}
