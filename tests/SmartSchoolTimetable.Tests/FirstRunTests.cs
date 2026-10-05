using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartSchoolTimetable.Infrastructure;

namespace SmartSchoolTimetable.Tests;

/// <summary>
/// First run (owner instruction, 2026-10-05): an empty database has no account at all and shows the setup screen;
/// no default account or credential may ever be shipped in the source (ADR 0025, withdrawn).
/// </summary>
public sealed partial class FirstRunTests
{
    private static readonly string[] ForbiddenStrings = ["Admin@12345", "DefaultOwner", "EnsureDefaultOwner"];
    private static readonly string[] ScannedExtensions = [".cs", ".json", ".ts", ".tsx", ".js", ".html", ".config"];

    [GeneratedRegex("\"[A-Za-z_]*password[A-Za-z_]*\"\\s*:\\s*\"[^\"]+\"", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordSetting();

    [Fact]
    public async Task AnEmptyDatabaseRequiresSetupAndHasNoUsers()
    {
        // TestHost loads the shipped appsettings.json, so a configured default account would show up here.
        await using var host = new TestHost();
        var bootstrap = await host.GetBootstrapAsync();
        Assert.True(bootstrap.SetupRequired);
        Assert.False(bootstrap.Authenticated);
        Assert.Null(bootstrap.Username);

        var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Data Source={host.DatabasePath};Pooling=False").Options;
        await using var db = new LocalDbContext(options);
        Assert.Equal(0, await db.Owners.CountAsync());
    }

    [Fact]
    public void NoDefaultCredentialAppearsInTheSource()
    {
        var root = TestPaths.FindRepositoryRoot();
        var offenders = new[] { Path.Combine(root, "src"), Path.Combine(root, "frontend", "src") }
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            .Where(path => ScannedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return ForbiddenStrings.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase))
                    || (Path.GetFileName(path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && PasswordSetting().IsMatch(text));
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        Assert.Empty(offenders);
    }
}
