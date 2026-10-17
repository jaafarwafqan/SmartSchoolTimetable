using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using SmartSchoolTimetable.Application.Sharing;

namespace SmartSchoolTimetable.Infrastructure.Sharing;

/// <summary>
/// «حفظ نسخة من مفتاح المدرسة» (DECISIONS_LOG #106): the school key in a file only the owner's password opens, for a new computer or a
/// restore DPAPI cannot read. PBKDF2-HMAC-SHA256 (600 000 iterations) derives 64 bytes: an AES-256-GCM key and a separate password-check
/// key, so a wrong password is told apart from a changed file. Every header byte is authenticated (GCM additional data).
/// <code>
/// "SSTK" | version 1 | kdf 1 | iterations (uint32 BE) | salt (16) | password check (16) | nonce (12) | ciphertext (92) | tag (16)
/// plaintext = school id (16, RFC 4122) | generation (int32 BE) | created at (int64 BE, Unix seconds) | public key (32) | private seed (32)
/// </code>
/// </summary>
public sealed class SchoolKeyBackupFile : ISchoolKeyBackupFile
{
    public const int Iterations = 600_000;
    public const int MaxIterations = 10_000_000;
    private const byte Version = 1;
    private const byte KdfPbkdf2Sha256 = 1;
    private const int SaltLength = 16;
    private const int CheckLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int HeaderLength = 4 + 1 + 1 + 4 + SaltLength + CheckLength + NonceLength;
    private const int PlainLength = 16 + 4 + 8 + Ed25519.PublicKeyLength + Ed25519.SeedLength;
    public const int FileLength = HeaderLength + PlainLength + TagLength;
    private static ReadOnlySpan<byte> Magic => "SSTK"u8;
    private static ReadOnlySpan<byte> CheckLabel => "SmartSchoolTimetable/sstkey/password-check"u8;

    public byte[] Seal(SchoolKeyBackup backup, string password)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(password);
        var file = new byte[FileLength];
        Magic.CopyTo(file);
        file[4] = Version;
        file[5] = KdfPbkdf2Sha256;
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(6), Iterations);
        RandomNumberGenerator.Fill(file.AsSpan(10, SaltLength));
        RandomNumberGenerator.Fill(file.AsSpan(10 + SaltLength + CheckLength, NonceLength));
        var (encryption, check) = Derive(password, file.AsSpan(10, SaltLength), Iterations);
        try
        {
            check.CopyTo(file.AsSpan(10 + SaltLength));
            var plain = new byte[PlainLength];
            backup.SchoolId.TryWriteBytes(plain.AsSpan(0, 16), bigEndian: true, out _);
            BinaryPrimitives.WriteInt32BigEndian(plain.AsSpan(16), backup.Generation);
            BinaryPrimitives.WriteInt64BigEndian(plain.AsSpan(20), backup.CreatedAt.ToUnixTimeSeconds());
            backup.PublicKey.CopyTo(plain.AsSpan(28));
            backup.PrivateSeed.CopyTo(plain.AsSpan(28 + Ed25519.PublicKeyLength));
            using var aes = new AesGcm(encryption, TagLength);
            aes.Encrypt(file.AsSpan(10 + SaltLength + CheckLength, NonceLength), plain, file.AsSpan(HeaderLength, PlainLength),
                file.AsSpan(HeaderLength + PlainLength, TagLength), file.AsSpan(0, HeaderLength));
            CryptographicOperations.ZeroMemory(plain);
            return file;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryption);
        }
    }

    public KeyBackupOpenResult Open(byte[] file, string password)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(password);
        if (file.Length != FileLength || !file.AsSpan(0, 4).SequenceEqual(Magic) || file[4] != Version || file[5] != KdfPbkdf2Sha256)
            return new(KeyBackupOpenStatus.Invalid, null);
        var iterations = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(6));
        // A later build may use more iterations; fewer than 600 000, or an absurd count, is refused.
        if (iterations is < Iterations or > MaxIterations)
            return new(KeyBackupOpenStatus.Invalid, null);
        var (encryption, check) = Derive(password, file.AsSpan(10, SaltLength), (int)iterations);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(check, file.AsSpan(10 + SaltLength, CheckLength)))
                return new(KeyBackupOpenStatus.WrongPassword, null);
            var plain = new byte[PlainLength];
            try
            {
                using var aes = new AesGcm(encryption, TagLength);
                aes.Decrypt(file.AsSpan(10 + SaltLength + CheckLength, NonceLength), file.AsSpan(HeaderLength, PlainLength),
                    file.AsSpan(HeaderLength + PlainLength, TagLength), plain, file.AsSpan(0, HeaderLength));
            }
            catch (AuthenticationTagMismatchException)
            {
                return new(KeyBackupOpenStatus.Tampered, null);
            }
            var schoolId = new Guid(plain.AsSpan(0, 16), bigEndian: true);
            var generation = BinaryPrimitives.ReadInt32BigEndian(plain.AsSpan(16));
            var createdAt = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64BigEndian(plain.AsSpan(20)));
            var publicKey = plain[28..(28 + Ed25519.PublicKeyLength)];
            var seed = plain[(28 + Ed25519.PublicKeyLength)..];
            CryptographicOperations.ZeroMemory(plain);
            // Authenticated, so only a writer with the password could have made it; still, the seed must give its public key.
            if (schoolId == Guid.Empty || generation < 1 || !Ed25519.PublicKeyOf(seed).AsSpan().SequenceEqual(publicKey))
                return new(KeyBackupOpenStatus.Invalid, null);
            return new(KeyBackupOpenStatus.Opened, new SchoolKeyBackup(schoolId, generation, createdAt, publicKey, seed));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryption);
        }
    }

    private static (byte[] Encryption, byte[] Check) Derive(string password, ReadOnlySpan<byte> salt, int iterations)
    {
        var bytes = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC)), salt, iterations, HashAlgorithmName.SHA256, 64);
        var check = HMACSHA256.HashData(bytes.AsSpan(32), CheckLabel)[..CheckLength];
        var encryption = bytes[..32];
        CryptographicOperations.ZeroMemory(bytes);
        return (encryption, check);
    }
}
