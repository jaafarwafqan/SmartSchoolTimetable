using SmartSchoolTimetable.Application.Common;
using SmartSchoolTimetable.Domain.Common;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Application.SchoolSetup;

public sealed class SchoolProfileService(IDataStore store, IAssetStore assets, TimeProvider clock)
{
    private static readonly SchoolProfileOptions Options = new(
        ApiText.Values<SchoolType>(),
        ApiText.Values<StudyType>(),
        ApiText.Values<NumeralSystem>(),
        ApiText.Values<CalendarDisplay>(),
        SchoolProfile.SupportedTimeZones);

    public async Task<SchoolProfileDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await LoadAsync(cancellationToken));

    public async Task<OperationResult<SchoolProfileDto>> UpdateAsync(
        UpdateSchoolProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = new InputErrors();
        var schoolType = input.Option<SchoolType>(command.SchoolType, nameof(command.SchoolType));
        var studyType = input.Option<StudyType>(command.StudyType, nameof(command.StudyType));
        var numerals = input.Option<NumeralSystem>(command.NumeralSystem, nameof(command.NumeralSystem));
        var calendar = input.Option<CalendarDisplay>(command.CalendarDisplay, nameof(command.CalendarDisplay));
        if (input.Any)
            return input.ToResult<SchoolProfileDto>();

        var profile = await LoadAsync(cancellationToken);
        if (!profile.IsVersion(command.Version))
            return OperationResult.Failure<SchoolProfileDto>(ErrorCodes.Conflict);
        try
        {
            profile.Update(
                command.Name,
                schoolType,
                studyType,
                command.PrincipalName,
                command.ScheduleOfficerName,
                command.TimeZone,
                numerals,
                calendar,
                clock.GetUtcNow());
        }
        catch (DomainValidationException exception)
        {
            return OperationResult.FromDomain<SchoolProfileDto>(exception);
        }

        AuditTrail.Record(store, clock, AuditEvents.SchoolProfileUpdated, "school-profile", "School profile updated.");
        return await SaveAsync(profile, cancellationToken);
    }

    public async Task<OperationResult<SchoolProfileDto>> UploadAssetAsync(
        string assetKind,
        ReadOnlyMemory<byte> content,
        string? declaredContentType,
        int version,
        CancellationToken cancellationToken)
    {
        const string field = "File";
        if (!ApiText.TryParse<SchoolAssetKind>(assetKind, out var kind))
            return OperationResult.Failure<SchoolProfileDto>(ErrorCodes.NotFound);
        if (content.IsEmpty)
            return OperationResult.Invalid<SchoolProfileDto>(field, ErrorCodes.Required);
        if (content.Length > ImageSignature.MaxBytes)
            return OperationResult.Invalid<SchoolProfileDto>(field, ErrorCodes.AssetTooLarge);
        var format = ImageSignature.Detect(content.Span);
        if (format is null)
            return OperationResult.Invalid<SchoolProfileDto>(field, ErrorCodes.AssetTypeNotAllowed);
        if (!ImageSignature.MatchesDeclared(format, declaredContentType))
            return OperationResult.Invalid<SchoolProfileDto>(field, ErrorCodes.AssetTypeMismatch);

        var profile = await LoadAsync(cancellationToken);
        if (!profile.IsVersion(version))
            return OperationResult.Failure<SchoolProfileDto>(ErrorCodes.Conflict);

        var prefix = ApiText.ToValue(kind);
        var storedName = await assets.SaveAsync(prefix, format.Extension, content, cancellationToken);
        var now = clock.GetUtcNow();
        var previous = profile.SetAsset(kind, new SchoolAsset(storedName, format.ContentType, content.Length, now), now);
        AuditTrail.Record(store, clock, AuditEvents.SchoolAssetUploaded, $"school-{prefix}", "School image uploaded.");
        var result = await SaveAsync(profile, cancellationToken, cleanupOnFailure: storedName);
        if (result.Succeeded && previous is not null)
            assets.Delete(previous);
        return result;
    }

    public async Task<OperationResult<SchoolProfileDto>> RemoveAssetAsync(
        string assetKind,
        int version,
        CancellationToken cancellationToken)
    {
        if (!ApiText.TryParse<SchoolAssetKind>(assetKind, out var kind))
            return OperationResult.Failure<SchoolProfileDto>(ErrorCodes.NotFound);
        var profile = await LoadAsync(cancellationToken);
        if (!profile.IsVersion(version))
            return OperationResult.Failure<SchoolProfileDto>(ErrorCodes.Conflict);
        if (profile.GetAsset(kind) is null)
            return OperationResult.Failure<SchoolProfileDto>(ErrorCodes.NotFound);

        var previous = profile.SetAsset(kind, null, clock.GetUtcNow());
        AuditTrail.Record(store, clock, AuditEvents.SchoolAssetRemoved, $"school-{ApiText.ToValue(kind)}", "School image removed.");
        var result = await SaveAsync(profile, cancellationToken);
        if (result.Succeeded && previous is not null)
            assets.Delete(previous);
        return result;
    }

    public async Task<AssetContent?> OpenAssetAsync(string assetKind, CancellationToken cancellationToken)
    {
        if (!ApiText.TryParse<SchoolAssetKind>(assetKind, out var kind))
            return null;
        var asset = (await LoadAsync(cancellationToken)).GetAsset(kind);
        if (asset is null || assets.OpenRead(asset.StoredFileName) is not { } stream)
            return null;
        return new AssetContent(stream, asset.ContentType, $"{ApiText.ToValue(kind)}.{asset.StoredFileName.Split('.')[^1]}");
    }

    internal async Task<SchoolProfile> LoadAsync(CancellationToken cancellationToken) =>
        await store.FirstOrDefaultAsync(store.Query<SchoolProfile>(), cancellationToken)
            ?? throw new InvalidOperationException("The school profile row is created at database initialization.");

    private async Task<OperationResult<SchoolProfileDto>> SaveAsync(
        SchoolProfile profile,
        CancellationToken cancellationToken,
        string? cleanupOnFailure = null)
    {
        try
        {
            await store.SaveChangesAsync(cancellationToken);
            return OperationResult.Success<SchoolProfileDto>(ToDto(profile));
        }
        catch (ConcurrencyConflictException)
        {
            if (cleanupOnFailure is not null)
                assets.Delete(cleanupOnFailure);
            return OperationResult.Failure<SchoolProfileDto>(ErrorCodes.Conflict);
        }
    }

    private static SchoolProfileDto ToDto(SchoolProfile profile) => new(
        profile.Name,
        ApiText.ToValue(profile.SchoolType),
        ApiText.ToValue(profile.StudyType),
        profile.PrincipalName,
        profile.ScheduleOfficerName,
        profile.TimeZoneId,
        ApiText.ToValue(profile.NumeralSystem),
        ApiText.ToValue(profile.CalendarDisplay),
        profile.Logo is not null,
        profile.Stamp is not null,
        profile.Version,
        Options);
}
