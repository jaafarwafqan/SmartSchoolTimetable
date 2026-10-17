using System.Runtime.Versioning;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using SmartSchoolTimetable.Application.Sharing;

namespace SmartSchoolTimetable.Infrastructure.Sharing;

/// <summary>Ed25519 (BouncyCastle, managed code) for phone packages (ADR 0056).</summary>
public static class Ed25519
{
    public const int SeedLength = 32;
    public const int PublicKeyLength = 32;
    public const int SignatureLength = 64;

    public static byte[] PublicKeyOf(byte[] seed) => new Ed25519PrivateKeyParameters(seed, 0).GeneratePublicKey().GetEncoded();

    public static byte[] Sign(byte[] seed, ReadOnlySpan<byte> message)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, new Ed25519PrivateKeyParameters(seed, 0));
        signer.BlockUpdate(message);
        return signer.GenerateSignature();
    }

    public static bool Verify(byte[] publicKey, ReadOnlySpan<byte> message, byte[] signature)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(signature);
        if (publicKey.Length != PublicKeyLength || signature.Length != SignatureLength)
            return false;
        try
        {
            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
            verifier.BlockUpdate(message);
            return verifier.VerifySignature(signature);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// The school key: a fresh Ed25519 seed from the OS random generator, protected with DPAPI for the current Windows user
/// (with a fixed application entropy). A blob made by another user or computer cannot be read here: <see cref="Unprotect"/> returns null.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSchoolKeyCrypto : ISchoolKeyCrypto
{
    private static readonly byte[] Entropy = "SmartSchoolTimetable/school-key/v1"u8.ToArray();

    public (byte[] PublicKey, byte[] PrivateSeed) Generate()
    {
        var seed = RandomNumberGenerator.GetBytes(Ed25519.SeedLength);
        return (Ed25519.PublicKeyOf(seed), seed);
    }

    public byte[] Protect(byte[] privateSeed) => ProtectedData.Protect(privateSeed, Entropy, DataProtectionScope.CurrentUser);

    public byte[]? Unprotect(byte[] protectedSeed)
    {
        try
        {
            var seed = ProtectedData.Unprotect(protectedSeed, Entropy, DataProtectionScope.CurrentUser);
            return seed.Length == Ed25519.SeedLength ? seed : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
