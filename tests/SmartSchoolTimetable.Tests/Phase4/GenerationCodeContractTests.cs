using System.Reflection;
using System.Text.RegularExpressions;
using SmartSchoolTimetable.Application.Generation;

namespace SmartSchoolTimetable.Tests.Phase4;

/// <summary>
/// The solver diagnostic codes and the verifier's violation codes are contracts like the HTTP error codes: one
/// definition file with a registry, an Arabic message and a TypeScript list entry for each, and no literal copies
/// anywhere else in the backend.
/// </summary>
public sealed partial class GenerationCodeContractTests
{
    private const string DefinitionPath = "src/SmartSchoolTimetable.Application/Generation/GenerationCodes.cs";

    public static bool IsDefinitionFile(string path) =>
        Path.GetRelativePath(TestPaths.FindRepositoryRoot(), path).Replace('\\', '/') == DefinitionPath;

    private static string[] Constants(Type type) => type.GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    public static TheoryData<string, string, string> Families => new()
    {
        { nameof(DiagnosticCodes), "  diagnostics: {", "features/generation/generationApi.ts" },
        { nameof(ViolationCodes), "  messages: {", "features/timetable/violationCodes.ts" },
    };

    private static IReadOnlyList<string> Registry(string family) =>
        family == nameof(DiagnosticCodes) ? DiagnosticCodes.All : ViolationCodes.All;

    private static Type TypeOf(string family) => family == nameof(DiagnosticCodes) ? typeof(DiagnosticCodes) : typeof(ViolationCodes);

    [Theory]
    [MemberData(nameof(Families))]
    public void EveryCodeIsRegisteredOnceAndHasAnArabicMessageAndAFrontendEntry(string family, string section, string list)
    {
        var registry = Registry(family);
        Assert.Equal(Constants(TypeOf(family)).Order(StringComparer.Ordinal), registry.Order(StringComparer.Ordinal));
        Assert.Equal(registry.Count, registry.Distinct(StringComparer.Ordinal).Count());
        var root = TestPaths.FindRepositoryRoot();
        var dictionary = File.ReadAllText(Path.Combine(root, "frontend", "src", "i18n", "ar", "phase4.ts"));
        var anchor = family == nameof(DiagnosticCodes) ? "export const generation" : "export const violations";
        var block = dictionary[dictionary.IndexOf(anchor, StringComparison.Ordinal)..];
        block = block[block.IndexOf(section, StringComparison.Ordinal)..];
        block = block[..block.IndexOf("\n  },", StringComparison.Ordinal)];
        var frontendList = File.ReadAllText(Path.Combine(root, "frontend", "src", list.Replace('/', Path.DirectorySeparatorChar)));
        foreach (var code in registry)
        {
            var entry = Regex.Match(block, $@"^\s{{4}}{code}:\s*(.+)$", RegexOptions.Multiline);
            Assert.True(entry.Success, $"{code} has no Arabic message.");
            Assert.Matches(@"[؀-ۿ]", entry.Groups[1].Value);
            Assert.Contains($"\"{code}\"", frontendList, StringComparison.Ordinal);
        }
        var keys = MessageKey().Matches(block).Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(registry.Order(StringComparer.Ordinal), keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoGenerationCodeLiteralAppearsOutsideItsDefinition()
    {
        var all = DiagnosticCodes.All.Concat(ViolationCodes.All).ToArray();
        var violations = TestPaths.BackendSourceFiles()
            .Where(file => !IsDefinitionFile(file))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, index)))
            .Where(item => all.Any(code => item.line.Contains($"\"{code}\"", StringComparison.Ordinal)))
            .Select(item => $"{Path.GetFileName(item.file)}:{item.index + 1}")
            .ToArray();
        Assert.Empty(violations);
        Assert.True(File.Exists(Path.Combine(TestPaths.FindRepositoryRoot(), DefinitionPath)));
    }

    [GeneratedRegex(@"^\s{4}([A-Z][A-Z0-9_]+):", RegexOptions.Multiline)]
    private static partial Regex MessageKey();
}
