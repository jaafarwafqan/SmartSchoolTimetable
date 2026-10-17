using SmartSchoolTimetable.Domain.Common;

namespace SmartSchoolTimetable.Domain.Sharing;

/// <summary>
/// The school's signing key for phone packages (exactly one row, Id = 1; ADR 0056). <see cref="SchoolId"/> is a random
/// identity made once and kept when the key is recreated, so a phone can tell "same school, new key" from "another
/// school". The private key is stored only as a DPAPI-protected blob (Windows, current user); the row lives in the
/// database, so every backup carries it.
/// </summary>
public sealed class SchoolKey : VersionedEntity
{
    public const long SingletonId = 1;
    public const int PublicKeyLength = 32;
    public const int MaxProtectedLength = 4096;

    private SchoolKey()
    {
    }

    public Guid SchoolId { get; private set; }

    /// <summary>The Ed25519 public key (32 bytes), given to phones in «رمز ربط المدرسة».</summary>
    public byte[] PublicKey { get; private set; } = [];

    /// <summary>The 32-byte Ed25519 private seed, protected with DPAPI. Never logged, never sent to the screen.</summary>
    public byte[] ProtectedPrivateKey { get; private set; } = [];

    /// <summary>1 for the first key, +1 each time «إعادة إنشاء مفتاح المدرسة» replaces it.</summary>
    public int Generation { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When «حفظ نسخة من مفتاح المدرسة» last saved THIS key to a password-protected file; null until then (the page then asks for it).</summary>
    public DateTimeOffset? BackedUpAt { get; private set; }

    public static SchoolKey Create(Guid schoolId, byte[] publicKey, byte[] protectedPrivateKey, DateTimeOffset now)
    {
        Validate(publicKey, protectedPrivateKey);
        new DomainErrors().When(schoolId == Guid.Empty, nameof(SchoolId), DomainErrorCode.Required).ThrowIfAny();
        return new SchoolKey
        {
            Id = SingletonId,
            SchoolId = schoolId,
            PublicKey = publicKey.ToArray(),
            ProtectedPrivateKey = protectedPrivateKey.ToArray(),
            Generation = 1,
            CreatedAt = now,
        };
    }

    /// <summary>A new key pair for the same school; every phone must scan the new «رمز ربط المدرسة».</summary>
    public void Replace(byte[] publicKey, byte[] protectedPrivateKey, DateTimeOffset now)
    {
        Validate(publicKey, protectedPrivateKey);
        PublicKey = publicKey.ToArray();
        ProtectedPrivateKey = protectedPrivateKey.ToArray();
        Generation++;
        CreatedAt = now;
        BackedUpAt = null;
        Touch();
    }

    public void MarkBackedUp(DateTimeOffset now)
    {
        BackedUpAt = now;
        Touch();
    }

    /// <summary>A key imported from its backup file (a new computer, or a restore DPAPI cannot read): the file's school id, generation and creation date.</summary>
    public static SchoolKey Restore(Guid schoolId, int generation, DateTimeOffset createdAt, byte[] publicKey, byte[] protectedPrivateKey, DateTimeOffset now)
    {
        var key = Create(schoolId, publicKey, protectedPrivateKey, createdAt);
        new DomainErrors().When(generation < 1, nameof(Generation), DomainErrorCode.OutOfRange).ThrowIfAny();
        key.Generation = generation;
        key.BackedUpAt = now;
        return key;
    }

    /// <summary>Replaces this row with an imported key (the same rules as <see cref="Restore"/>).</summary>
    public void ReplaceWith(SchoolKey imported)
    {
        ArgumentNullException.ThrowIfNull(imported);
        SchoolId = imported.SchoolId;
        PublicKey = imported.PublicKey.ToArray();
        ProtectedPrivateKey = imported.ProtectedPrivateKey.ToArray();
        Generation = imported.Generation;
        CreatedAt = imported.CreatedAt;
        BackedUpAt = imported.BackedUpAt;
        Touch();
    }

    private static void Validate(byte[] publicKey, byte[] protectedPrivateKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(protectedPrivateKey);
        new DomainErrors()
            .When(publicKey.Length != PublicKeyLength, nameof(PublicKey), DomainErrorCode.InvalidOption)
            .When(protectedPrivateKey.Length is 0 or > MaxProtectedLength, nameof(ProtectedPrivateKey), DomainErrorCode.InvalidOption)
            .ThrowIfAny();
    }
}
