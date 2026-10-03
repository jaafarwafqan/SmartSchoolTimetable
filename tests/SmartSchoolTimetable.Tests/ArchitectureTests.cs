using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SmartSchoolTimetable.Api;
using SmartSchoolTimetable.Application;
using SmartSchoolTimetable.Domain;

namespace SmartSchoolTimetable.Tests;

public sealed class ArchitectureTests
{
    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly ApiAssembly = typeof(AuthEndpoints).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ILocalAuthService).Assembly;
    private static readonly Assembly DomainAssembly = typeof(OwnerAccount).Assembly;

    [Fact]
    public void ApiDoesNotReferenceDomainOrEntityFrameworkCore()
    {
        var references = ReferencedAssemblyNames(ApiAssembly);
        Assert.DoesNotContain(DomainAssembly.GetName().Name!, references);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void EndpointsAndOtherApiTypesDoNotUseDbContextOrDomainEntities()
    {
        var violations = ApiAssembly.GetTypes()
            .SelectMany(type => ReferencedTypes(type).Select(referenced => (type, referenced)))
            .Where(pair =>
                pair.referenced.Assembly == DomainAssembly ||
                typeof(DbContext).IsAssignableFrom(pair.referenced))
            .Select(pair => $"{pair.type.FullName} -> {pair.referenced.FullName}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(violations);

        var apiDirectory = Path.Combine(TestPaths.FindRepositoryRoot(), "src", "SmartSchoolTimetable.Api");
        var sourceViolations = Directory.EnumerateFiles(apiDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains("DbContext", StringComparison.Ordinal) ||
                    text.Contains("SmartSchoolTimetable.Domain", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToArray();
        Assert.Empty(sourceViolations);
    }

    [Fact]
    public void OrToolsIsNotUsedByDomainOrApplication()
    {
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly })
        {
            Assert.DoesNotContain(ReferencedAssemblyNames(assembly), name =>
                name.StartsWith("Google.OrTools", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                assembly.GetTypes().SelectMany(ReferencedTypes),
                type => type.Namespace?.StartsWith("Google.OrTools", StringComparison.Ordinal) == true);
        }
    }

    /// <summary>
    /// Feature folders depend inward only: a Domain feature may use the shared Common/Text/SchoolSetup structure;
    /// an Application feature may use Application.Common and the shared SchoolSetup context, never another
    /// feature. Dashboard is the documented exception: it is a read model over every feature.
    /// </summary>
    [Fact]
    public void FeatureFoldersRespectDependencyDirection()
    {
        var domainViolations = FeatureViolations(DomainAssembly, "SmartSchoolTimetable.Domain", ["Common", "Text", "SchoolSetup"], []);
        var applicationViolations = FeatureViolations(ApplicationAssembly, "SmartSchoolTimetable.Application", ["Common", "SchoolSetup"], ["Dashboard"]);
        Assert.Empty(domainViolations);
        Assert.Empty(applicationViolations);
    }

    private static string[] FeatureViolations(Assembly assembly, string root, string[] shared, string[] readModels)
    {
        static string? FeatureOf(Type type, string root) =>
            type.Namespace is { } ns && ns.StartsWith(root + ".", StringComparison.Ordinal)
                ? ns[(root.Length + 1)..].Split('.')[0]
                : null;

        return assembly.GetTypes()
            .Select(type => (type, feature: FeatureOf(type, root)))
            .Where(pair => pair.feature is not null && !shared.Contains(pair.feature) && !readModels.Contains(pair.feature))
            .SelectMany(pair => ReferencedTypes(pair.type)
                .Where(referenced => referenced.Assembly == assembly)
                .Select(referenced => (pair.type, pair.feature, referenced, target: FeatureOf(referenced, root))))
            .Where(item => item.target is not null && item.target != item.feature && !shared.Contains(item.target))
            .Select(item => $"{item.type.FullName} -> {item.referenced.FullName}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var direct = new List<Type>();
        if (type.BaseType is not null)
            direct.Add(type.BaseType);
        direct.AddRange(type.GetInterfaces());
        direct.AddRange(type.GetFields(AllMembers).Select(field => field.FieldType));
        direct.AddRange(type.GetProperties(AllMembers).Select(property => property.PropertyType));
        foreach (var method in type.GetMethods(AllMembers))
        {
            direct.Add(method.ReturnType);
            direct.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
        }
        foreach (var constructor in type.GetConstructors(AllMembers))
            direct.AddRange(constructor.GetParameters().Select(parameter => parameter.ParameterType));

        return direct.SelectMany(Flatten).Distinct();
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (var inner in Flatten(element))
                yield return inner;
            yield break;
        }

        yield return type;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments().SelectMany(Flatten))
                yield return argument;
    }
}
