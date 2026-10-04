using Microsoft.Extensions.DependencyInjection;
using SmartSchoolTimetable.Application.Calendar;
using SmartSchoolTimetable.Application.SchoolSetup;
using SmartSchoolTimetable.Application.Stages;
using SmartSchoolTimetable.Application.Subjects;
using SmartSchoolTimetable.Application.Teachers;

namespace SmartSchoolTimetable.Infrastructure.DemoData;

/// <summary>
/// <c>--seed-demo-data &lt;new-db-path&gt; [--dual-shift]</c>: creates a SEPARATE database with a fictional Arabic
/// sample school. It refuses an existing file and the protected paths (the default %LOCALAPPDATA% database and the
/// configured one), so the owner's real data is never touched. Every record goes through the Application services,
/// so all Domain rules and audit entries apply. Teacher workload is Phase 3 and is not created.
/// </summary>
public static class DemoDataSeeder
{
    public const string RefusedExistingMessage = "الملف موجود مسبقاً. اختر مساراً لملف جديد غير موجود.";
    public const string RefusedProtectedMessage = "لا يمكن إنشاء البيانات التجريبية في قاعدة البيانات الحقيقية. اختر مساراً منفصلاً.";
    public const string MissingPathMessage = "حدد مسار ملف قاعدة البيانات التجريبية الجديد بعد --seed-demo-data.";
    public const string CompletedMessage = "تم إنشاء قاعدة بيانات تجريبية في:";

    /// <summary>Null when the path may be used; otherwise the Arabic reason for refusing it.</summary>
    public static string? CheckTarget(string? targetPath, IEnumerable<string> protectedPaths)
    {
        ArgumentNullException.ThrowIfNull(protectedPaths);
        if (string.IsNullOrWhiteSpace(targetPath) || targetPath.StartsWith("--", StringComparison.Ordinal))
            return MissingPathMessage;
        var fullPath = Path.GetFullPath(targetPath);
        if (protectedPaths.Any(path => string.Equals(Path.GetFullPath(path), fullPath, StringComparison.OrdinalIgnoreCase)))
            return RefusedProtectedMessage;
        return File.Exists(fullPath) || File.Exists($"{fullPath}-wal") ? RefusedExistingMessage : null;
    }

    /// <returns>True when the database was created.</returns>
    public static async Task<bool> SeedAsync(string? targetPath, IEnumerable<string> protectedPaths, bool dualShift, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (CheckTarget(targetPath, protectedPaths) is { } refusal)
        {
            await output.WriteLineAsync(refusal);
            return false;
        }
        var fullPath = Path.GetFullPath(targetPath!);
        var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddScoped<SchoolProfileService>()
            .AddScoped<IYearStructure, YearStructureService>()
            .AddScoped<AcademicYearService>()
            .AddScoped<TimetableStructureService>()
            .AddScoped<StagesSectionsService>()
            .AddScoped<SubjectsService>()
            .AddScoped<TeachersService>()
            .AddScoped<CalendarService>()
            .AddLocalInfrastructure(fullPath, skipLoginDelay: true);
        await using var provider = services.BuildServiceProvider();
        await LocalInfrastructureRegistration.InitializeLocalDatabaseAsync(provider, cancellationToken);
        await using (var scope = provider.CreateAsyncScope())
        {
            await new DemoSchool(scope.ServiceProvider, cancellationToken).CreateAsync(dualShift);
        }
        await output.WriteLineAsync($"{CompletedMessage} {fullPath}");
        return true;
    }
}
