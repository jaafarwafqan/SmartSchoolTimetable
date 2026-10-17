using System.Buffers.Binary;
using SmartSchoolTimetable.Application.Sharing;
using SmartSchoolTimetable.Infrastructure.Sharing;

namespace SmartSchoolTimetable.Tests.Phase9;

/// <summary>«حفظ نسخة من مفتاح المدرسة» (DECISIONS_LOG #106): the password-protected .sstkey file, at its real work factor.</summary>
public sealed class SchoolKeyBackupFileTests
{
    private const string Password = "كلمة-مرور-تجريبية-123";
    private static readonly SchoolKeyBackupFile Files = new();
    private static readonly SchoolKeyBackup Backup = new(SstVectors.SchoolA, 3, new DateTimeOffset(2026, 10, 1, 7, 0, 0, TimeSpan.Zero),
        Ed25519.PublicKeyOf(SstVectors.SeedA), SstVectors.SeedA.ToArray());

    [Fact]
    public void ARoundTripGivesBackTheSameKeyAndEveryFileIsDifferent()
    {
        var file = Files.Seal(Backup, Password);
        Assert.Equal(SchoolKeyBackupFile.FileLength, file.Length);
        Assert.Equal("SSTK"u8.ToArray(), file[..4]);
        Assert.Equal((uint)SchoolKeyBackupFile.Iterations, BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(6)));
        Assert.False(file.AsSpan().IndexOf(SstVectors.SeedA) >= 0);

        var opened = Files.Open(file, Password);
        Assert.Equal(KeyBackupOpenStatus.Opened, opened.Status);
        Assert.Equal((Backup.SchoolId, Backup.Generation, Backup.CreatedAt), (opened.Backup!.SchoolId, opened.Backup.Generation, opened.Backup.CreatedAt));
        Assert.Equal(Backup.PublicKey, opened.Backup.PublicKey);
        Assert.Equal(SstVectors.SeedA, opened.Backup.PrivateSeed);
        // A fresh salt and nonce every time.
        Assert.NotEqual(file, Files.Seal(Backup, Password));
        // The password is compared after Unicode normalisation (the same Arabic text typed two ways).
        Assert.Equal(KeyBackupOpenStatus.Opened, Files.Open(Files.Seal(Backup, "إم"), "إم").Status);
    }

    [Fact]
    public void AWrongPasswordIsToldApartFromAChangedFile()
    {
        var file = Files.Seal(Backup, Password);
        Assert.Equal(KeyBackupOpenStatus.WrongPassword, Files.Open(file, Password + "x").Status);
        Assert.Equal(KeyBackupOpenStatus.WrongPassword, Files.Open(file, "").Status);

        // Any change after the password check (nonce, ciphertext, tag) is "tampered"; a changed salt or check fails as a wrong password.
        foreach (var index in new[] { 42, 54, 80, SchoolKeyBackupFile.FileLength - 1 })
        {
            var copy = file.ToArray();
            copy[index] ^= 0x01;
            Assert.Equal(KeyBackupOpenStatus.Tampered, Files.Open(copy, Password).Status);
        }
        foreach (var index in new[] { 10, 26 })
        {
            var copy = file.ToArray();
            copy[index] ^= 0x01;
            Assert.Equal(KeyBackupOpenStatus.WrongPassword, Files.Open(copy, Password).Status);
        }
    }

    [Fact]
    public void NotAKeyFileOrAWeakenedOneIsInvalid()
    {
        var file = Files.Seal(Backup, Password);
        Assert.Equal(KeyBackupOpenStatus.Invalid, Files.Open([], Password).Status);
        Assert.Equal(KeyBackupOpenStatus.Invalid, Files.Open(file[..^1], Password).Status);
        Assert.Equal(KeyBackupOpenStatus.Invalid, Files.Open([(byte)'X', .. file[1..]], Password).Status);
        Assert.Equal(KeyBackupOpenStatus.Invalid, Files.Open([.. file[..4], 2, .. file[5..]], Password).Status);
        Assert.Equal(KeyBackupOpenStatus.Invalid, Files.Open([.. file[..5], 2, .. file[6..]], Password).Status);
        var weak = file.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(weak.AsSpan(6), 1000);
        Assert.Equal(KeyBackupOpenStatus.Invalid, Files.Open(weak, Password).Status);
        Assert.Equal(KeyBackupOpenStatus.Invalid, Files.Open(new byte[SchoolKeyBackupFile.FileLength], Password).Status);
    }

    [Theory]
    [InlineData("أحمد علي حسن", "أحمد علي حسن")]
    [InlineData("سعد: كاظم / حسن?", "سعد- كاظم - حسن-")]
    [InlineData("  ..مريم  جاسم..  ", "مريم جاسم")]
    [InlineData("CON", "_CON")]
    [InlineData("nul.txt", "_nul.txt")]
    [InlineData("‏علي‮", "-علي-")]
    [InlineData("...", "teacher")]
    [InlineData("a\tb\u0000c", "a-b-c")]
    public void TeacherFileNamesAreSafeOnWindowsAndroidAndIos(string name, string expected) =>
        Assert.Equal(expected, SharingService.SafeFileName(name));

    [Fact]
    public void ALongNameIsCutWithoutBreakingACharacter()
    {
        var name = new string('م', 70) + "😀😀😀😀😀😀😀😀";
        var safe = SharingService.SafeFileName(name);
        Assert.True(safe.Length <= SharingService.MaxFileNameLength);
        Assert.False(char.IsHighSurrogate(safe[^1]));
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(safe + "-v12.sst") < 255);
    }
}
