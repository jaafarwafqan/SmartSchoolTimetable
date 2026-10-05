using System.Reflection;
using System.Text.RegularExpressions;
using SmartSchoolTimetable.Application.Scheduling;

namespace SmartSchoolTimetable.Tests;

/// <summary>
/// The pre-solve finding codes (Phase 3 §3) are a contract like the HTTP error codes: one definition site with a
/// registry, an Arabic message and a TypeScript union entry for each, and no literal copies anywhere else.
/// </summary>
public sealed partial class FindingCodeContractTests
{
    private const string DefinitionPath = "src/SmartSchoolTimetable.Application/Scheduling/FindingCodes.cs";

    private static readonly string[] Constants = typeof(FindingCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    public static bool IsDefinitionFile(string path) =>
        Path.GetRelativePath(TestPaths.FindRepositoryRoot(), path).Replace('\\', '/') == DefinitionPath;

    [Fact]
    public void EveryConstantIsInTheRegistryExactlyOnce()
    {
        Assert.Equal(19, Constants.Length);
        Assert.Equal(Constants.Order(StringComparer.Ordinal), FindingCodes.All.Order(StringComparer.Ordinal));
        Assert.Equal(FindingCodes.All.Count, FindingCodes.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(FindingCodes.All, code => Assert.Matches(UpperSnake(), code));
    }

    [Fact]
    public void EveryCodeHasAnArabicMessageAndAUnionEntryInTheFrontend()
    {
        var root = TestPaths.FindRepositoryRoot();
        var dictionary = File.ReadAllText(Path.Combine(root, "frontend", "src", "i18n", "ar", "phase3.ts"));
        var messages = dictionary[dictionary.IndexOf("  messages: {", StringComparison.Ordinal)..];
        messages = messages[..messages.IndexOf("\n  },", StringComparison.Ordinal)];
        var union = File.ReadAllText(Path.Combine(root, "frontend", "src", "features", "readiness", "readinessApi.ts"));
        foreach (var code in FindingCodes.All)
        {
            var entry = Regex.Match(messages, $@"^\s{{4}}{code}:\s*(.+)$", RegexOptions.Multiline);
            Assert.True(entry.Success, $"{code} has no Arabic message.");
            Assert.Matches(@"[؀-ۿ]", entry.Groups[1].Value);
            Assert.Contains($"\"{code}\"", union, StringComparison.Ordinal);
        }
        // Nothing extra on the frontend either.
        var frontendCodes = MessageKey().Matches(messages).Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(FindingCodes.All.Order(StringComparer.Ordinal), frontendCodes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoFindingCodeLiteralAppearsOutsideItsDefinition()
    {
        var violations = TestPaths.BackendSourceFiles()
            .Where(file => !IsDefinitionFile(file))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, index)))
            .Where(item => FindingCodes.All.Any(code => item.line.Contains($"\"{code}\"", StringComparison.Ordinal)))
            .Select(item => $"{Path.GetFileName(item.file)}:{item.index + 1}")
            .ToArray();
        Assert.Empty(violations);
        Assert.True(File.Exists(Path.Combine(TestPaths.FindRepositoryRoot(), DefinitionPath)));
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$")]
    private static partial Regex UpperSnake();

    [GeneratedRegex(@"^\s{4}([A-Z][A-Z0-9_]+):", RegexOptions.Multiline)]
    private static partial Regex MessageKey();
}
