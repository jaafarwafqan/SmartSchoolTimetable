using System.Security.Cryptography;
using System.Text.Json;
using SmartSchoolTimetable.Infrastructure.Sharing;

namespace SmartSchoolTimetable.Tests.Phase9;

/// <summary>
/// The shared vectors in tests/vectors read as the manifest says, with the reference reader. To regenerate them after a deliberate
/// format change, run the tests with WRITE_SST_VECTORS=1 (then copy the folder into the phone app's repository).
/// </summary>
public sealed class SstVectorTests
{
    private static string Folder => Path.Combine(TestPaths.FindRepositoryRoot(), "tests", "vectors");

    [Fact]
    public void EveryVectorReadsAsTheManifestSaysAndMatchesItsChecksum()
    {
        if (Environment.GetEnvironmentVariable("WRITE_SST_VECTORS") == "1")
            SstVectors.WriteAll(Folder);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, "vectors.json")));
        var root = manifest.RootElement;
        var today = DateOnly.Parse(root.GetProperty("today").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var trustedJson = root.GetProperty("trustedSchool");
        var trusted = new TrustedSchool(trustedJson.GetProperty("schoolId").GetGuid(), Convert.FromHexString(trustedJson.GetProperty("publicKeyHex").GetString()!));
        Assert.Equal(SstVectors.TrustedA.PublicKey, trusted.PublicKey);

        var vectors = root.GetProperty("vectors").EnumerateArray().ToArray();
        Assert.Equal(SstVectors.All.Select(vector => vector.File), vectors.Select(vector => vector.GetProperty("file").GetString()));
        foreach (var vector in vectors)
        {
            var name = vector.GetProperty("file").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(Folder, name));
            Assert.Equal(vector.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            var result = PhonePackageReader.Read(bytes, vector.GetProperty("trusted").GetBoolean() ? trusted : null, today);
            Assert.True(JsonNamingPolicy.CamelCase.ConvertName(result.Status.ToString()) == vector.GetProperty("expected").GetString(), $"{name}: {result.Status}");
            // One scan: an unpaired read proposes school A exactly when the manifest says so.
            Assert.Equal(vector.GetProperty("pairs").GetBoolean(), result.NewTrust is not null);
            if (result.NewTrust is { } proposed)
                Assert.Equal((trusted.SchoolId, Convert.ToHexString(trusted.PublicKey)), (proposed.SchoolId, Convert.ToHexString(proposed.PublicKey)));
            if (vector.GetProperty("expectedJson").GetString() is { } expected)
                Assert.Equal(File.ReadAllText(Path.Combine(Folder, expected)).ReplaceLineEndings("\n"), SstVectors.ToJson(result.Package!));
        }

        // The QR text is the teacher package as unpadded base64url.
        var qr = File.ReadAllText(Path.Combine(Folder, "valid-teacher.qr.txt")).Trim();
        Assert.Matches("^[A-Za-z0-9_-]+$", qr);
        Assert.Equal(File.ReadAllBytes(Path.Combine(Folder, "valid-teacher.sst")), PhonePackageFormat.FromQrText(qr));

        // SHA256SUMS lists every vector file (the phone repository checks its copy against it).
        var sums = File.ReadAllLines(Path.Combine(Folder, "SHA256SUMS")).Where(line => line.Length > 0).Select(line => line.Split("  ")).ToArray();
        Assert.All(sums, pair => Assert.Equal(pair[0], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Folder, pair[1])))).ToLowerInvariant()));
        Assert.Equal(Directory.EnumerateFiles(Folder).Select(Path.GetFileName).Where(file => file is not "SHA256SUMS" and not "README.md").Order(StringComparer.Ordinal),
            sums.Select(pair => pair[1]));
    }

    [Fact]
    public void TheVectorsDecodeToTheFixtureContentSoTheirMeaningIsPinned()
    {
        var teacher = PhonePackageReader.Read(File.ReadAllBytes(Path.Combine(Folder, "valid-teacher.sst")), SstVectors.TrustedA, SstVectors.Today);
        Assert.Equal(SstVectors.ToJson(SstVectors.Teacher(SstVectors.SchoolA, SstVectors.SeedA)), SstVectors.ToJson(teacher.Package!));
        var content = teacher.Package!.Timetable!;
        Assert.Single(content.Teachers);
        Assert.Equal(30, content.Lessons.Count);
        Assert.Equal([1, 3], content.Sessions.Select(session => session.Session));
        Assert.Equal(10, content.SessionDays.Count);
    }
}
