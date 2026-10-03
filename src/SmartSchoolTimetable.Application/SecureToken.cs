using System.Buffers.Text;
using System.Security.Cryptography;

namespace SmartSchoolTimetable.Application;

public static class SecureToken
{
    public const int DefaultByteCount = 32;

    /// <summary>Creates a cryptographically random, URL-safe (base64url, unpadded) token.</summary>
    public static string CreateUrlSafe(int byteCount = DefaultByteCount) =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(byteCount));
}
