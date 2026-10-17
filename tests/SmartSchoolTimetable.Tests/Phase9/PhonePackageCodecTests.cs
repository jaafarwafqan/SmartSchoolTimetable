using System.IO.Compression;
using SmartSchoolTimetable.Application.Sharing;
using SmartSchoolTimetable.Infrastructure.Sharing;

namespace SmartSchoolTimetable.Tests.Phase9;

/// <summary>The .sst envelope and payload (ADR 0056): round trip, and every way a package is refused.</summary>
public sealed class PhonePackageCodecTests
{
    private static readonly PhonePackageWriter Writer = new();

    private static byte[] ValidTeacher() => Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA), SstVectors.SeedA);

    private static PackageReadStatus Read(byte[] bytes) => PhonePackageReader.Read(bytes, SstVectors.TrustedA, SstVectors.Today).Status;

    [Fact]
    public void APackageRoundTripsAndTheSameInputGivesTheSameBytes()
    {
        var bytes = ValidTeacher();
        Assert.Equal(bytes, ValidTeacher());
        Assert.Equal("SST"u8.ToArray(), bytes[..3]);
        Assert.Equal(1, bytes[3]);
        var result = PhonePackageReader.Read(bytes, SstVectors.TrustedA, SstVectors.Today);
        Assert.Equal(PackageReadStatus.Valid, result.Status);
        Assert.Equal(SstVectors.ToJson(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA)), SstVectors.ToJson(result.Package!));
        Assert.Equal(bytes, PhonePackageFormat.FromQrText(PhonePackageFormat.ToQrText(bytes)));
    }

    [Fact]
    public void ChangingAnySingleByteAfterTheEnvelopeHeadIsDetected()
    {
        var bytes = ValidTeacher();
        for (var index = 4; index < bytes.Length; index++)
        {
            var copy = bytes.ToArray();
            copy[index] ^= 0x01;
            // A flip inside the compressed body may even decode to another school id or key id; it is never accepted.
            Assert.NotEqual(PackageReadStatus.Valid, Read(copy));
            Assert.NotEqual(PackageReadStatus.Expired, Read(copy));
        }
    }

    [Fact]
    public void NotAPackageNewerEnvelopeAndOversizedInputAreRefusedBeforeAnyParsing()
    {
        var bytes = ValidTeacher();
        Assert.Equal(PackageReadStatus.Malformed, Read([]));
        Assert.Equal(PackageReadStatus.Malformed, Read(bytes[..PhonePackageFormat.BodyOffset]));
        Assert.Equal(PackageReadStatus.Malformed, Read([(byte)'X', .. bytes[1..]]));
        Assert.Equal(PackageReadStatus.Malformed, Read([.. bytes[..3], 0, .. bytes[4..]]));
        Assert.Equal(PackageReadStatus.NewerFormat, Read([.. bytes[..3], 2, .. bytes[4..]]));
        Assert.Equal(PackageReadStatus.Malformed, Read([.. bytes, .. new byte[PhonePackageFormat.MaxPackageBytes]]));
    }

    [Fact]
    public void ADecompressionBombIsRefused()
    {
        // 3 MiB of zeros compress to a few KiB; the reader stops at 2 MiB.
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(new byte[3 * 1024 * 1024]);
        byte[] head = [.. "SST"u8, 1];
        var body = compressed.ToArray();
        var signature = Ed25519.Sign(SstVectors.SeedA, PhonePackageFormat.SignedMessage(head, body));
        Assert.Equal(PackageReadStatus.Tampered, Read([.. head, .. signature, .. body]));
    }

    [Fact]
    public void ALessonPointingOutsideItsListsIsRefusedEvenWhenSigned()
    {
        var content = SstVectors.TeacherContent();
        var broken = content with { Lessons = [.. content.Lessons, new PackageLesson(7, 1, 0, 0, 5)] };
        Assert.Equal(PackageReadStatus.Tampered, Read(Writer.Write(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA, broken), SstVectors.SeedA)));
    }

    [Fact]
    public void APairingCodeIsSelfSignedAndItsKeyIdMustMatchItsKey()
    {
        var pairing = Writer.Write(SstVectors.Pairing(SstVectors.SchoolA, SstVectors.SeedA), SstVectors.SeedA);
        var read = PhonePackageReader.Read(pairing, null, SstVectors.Today);
        Assert.Equal(PackageReadStatus.Valid, read.Status);
        Assert.Equal(SstVectors.TrustedA.PublicKey, read.Package!.PublicKey);

        // Another key claiming school A's key id, or school A's key with a forged id: refused.
        var forged = SstVectors.Pairing(SstVectors.SchoolA, SstVectors.SeedB) with { Header = SstVectors.Pairing(SstVectors.SchoolA, SstVectors.SeedA).Header };
        Assert.Equal(PackageReadStatus.Tampered, PhonePackageReader.Read(Writer.Write(forged, SstVectors.SeedB), null, SstVectors.Today).Status);
    }

    [Fact]
    public void TheLastDayOfTheYearIsStillValidAndTheNextDayIsExpired()
    {
        var bytes = ValidTeacher();
        Assert.Equal(PackageReadStatus.Valid, PhonePackageReader.Read(bytes, SstVectors.TrustedA, new DateOnly(2027, 6, 30)).Status);
        Assert.Equal(PackageReadStatus.Expired, PhonePackageReader.Read(bytes, SstVectors.TrustedA, new DateOnly(2027, 7, 1)).Status);
    }

    [Fact]
    public void ATeacherPackageOfATypicalSchoolFitsTheQrBudget()
    {
        // 30 lessons, double shift, three holidays: the QR text fits a version-25 QR at level M (997 bytes in byte mode).
        var text = PhonePackageFormat.ToQrText(ValidTeacher());
        Assert.True(text.Length <= 997, $"QR text is {text.Length} characters.");
    }
}
