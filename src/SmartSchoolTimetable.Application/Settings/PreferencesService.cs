using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.Settings;

namespace SmartSchoolTimetable.Application.Settings;

public sealed record PrintSettingDto(string Paper, string Orientation);

/// <param name="Theme">system, light or dark.</param>
/// <param name="DefaultSemester">1 or 2, or null to follow the year's dates.</param>
public sealed record PreferencesDto(string Theme, int? DefaultSemester, PrintSettingDto Section, PrintSettingDto Teacher, PrintSettingDto School, bool PrintFit, int Version);

public sealed record PrintSettingInput(string? Paper, string? Orientation);

public sealed record SavePreferencesCommand(string? Theme, int? DefaultSemester, PrintSettingInput? Section, PrintSettingInput? Teacher, PrintSettingInput? School, bool PrintFit, int Version);

/// <summary>The owner's stored preferences (M2): theme, default semester, default print options. Versioned like every editable record.</summary>
public sealed class PreferencesService(IDataStore store, TimeProvider clock)
{
    public async Task<PreferencesDto> GetAsync(CancellationToken token) => ToDto(await LoadAsync(token));

    public async Task<OperationResult<PreferencesDto>> UpdateAsync(SavePreferencesCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var theme = input.Option<ThemePreference>(command.Theme, "Theme");
        var section = Print(command.Section, "Section", input);
        var teacher = Print(command.Teacher, "Teacher", input);
        var school = Print(command.School, "School", input);
        if (command.DefaultSemester is not null and not (1 or 2))
            input.Add("DefaultSemester", ErrorCodes.InvalidOption);
        if (input.Any)
            return input.ToResult<PreferencesDto>();

        var preferences = await LoadAsync(token);
        if (!preferences.IsVersion(command.Version))
            return OperationResult.Failure<PreferencesDto>(ErrorCodes.Conflict);
        var changed = false;
        try
        {
            changed = preferences.Update(theme, command.DefaultSemester, section, teacher, school, command.PrintFit);
        }
        catch (DomainValidationException exception)
        {
            return OperationResult.FromDomain<PreferencesDto>(exception);
        }
        if (changed)
            AuditTrail.Record(store, clock, AuditEvents.PreferencesUpdated, "preferences", "Preferences updated.");
        return await store.SaveAsync(() => ToDto(preferences), "Theme", token);
    }

    private static PrintSetting Print(PrintSettingInput? value, string field, InputErrors input) =>
        new(input.Option<PaperSize>(value?.Paper, field + "Paper"), input.Option<PageOrientation>(value?.Orientation, field + "Orientation"));

    /// <summary>The row is created at start-up; a missing row (an old test database) is created on first use.</summary>
    private async Task<AppPreferences> LoadAsync(CancellationToken token)
    {
        if (await store.FirstOrDefaultAsync(store.Query<AppPreferences>(), token) is { } preferences)
            return preferences;
        preferences = AppPreferences.CreateDefault();
        store.Add(preferences);
        await store.SaveChangesAsync(token);
        return preferences;
    }

    private static PreferencesDto ToDto(AppPreferences preferences) => new(
        ApiText.ToValue(preferences.Theme), preferences.DefaultSemester, ToDto(preferences.Section), ToDto(preferences.Teacher), ToDto(preferences.School),
        preferences.PrintFit, preferences.Version);

    private static PrintSettingDto ToDto(PrintSetting setting) => new(ApiText.ToValue(setting.Paper), ApiText.ToValue(setting.Orientation));
}
