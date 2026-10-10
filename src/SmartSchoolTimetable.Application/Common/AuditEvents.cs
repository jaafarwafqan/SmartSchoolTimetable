namespace SmartSchoolTimetable.Application.Common;

/// <summary>
/// The filter groups of the local audit history («سجل التغييرات»). Each event belongs to exactly one.
/// </summary>
public static class AuditCategories
{
    public const string Timetable = "timetable";
    public const string Generation = "generation";
    public const string Backup = "backup";
    public const string Account = "account";
    public const string Settings = "settings";
    public const string Import = "import";
    public const string School = "school";

    public static readonly IReadOnlyList<string> All = [Timetable, Generation, Backup, Account, Settings, Import, School];
}

/// <summary>
/// Every audit event type. The history stores the event code and its parameters, never user-readable text: the
/// frontend maps each code to an Arabic sentence (enforced by AuditEventContractTests). Add a new event here, in
/// <see cref="CategoryOf"/> and in the Arabic dictionary.
/// </summary>
public static class AuditEvents
{
    public const string AcademicYearCreated = "AcademicYearCreated";
    public const string AcademicYearDeleted = "AcademicYearDeleted";
    public const string AcademicYearMadeCurrent = "AcademicYearMadeCurrent";
    public const string AcademicYearUpdated = "AcademicYearUpdated";
    public const string BackupCreated = "BackupCreated";
    public const string BackupRestored = "BackupRestored";
    public const string BellSettingsUpdated = "BellSettingsUpdated";
    public const string CalendarDayCreated = "CalendarDayCreated";
    public const string CalendarDayDeleted = "CalendarDayDeleted";
    public const string CalendarDayUpdated = "CalendarDayUpdated";
    public const string CurriculumCopied = "CurriculumCopied";
    public const string CurriculumEntryArchived = "CurriculumEntryArchived";
    public const string CurriculumEntryCleared = "CurriculumEntryCleared";
    public const string CurriculumEntryCreated = "CurriculumEntryCreated";
    public const string CurriculumEntryDeleted = "CurriculumEntryDeleted";
    public const string CurriculumEntryRestored = "CurriculumEntryRestored";
    public const string CurriculumEntryUpdated = "CurriculumEntryUpdated";
    public const string CurriculumLessonsSet = "CurriculumLessonsSet";
    public const string GenerationCancelled = "GenerationCancelled";
    public const string GenerationFinished = "GenerationFinished";
    public const string GenerationInterrupted = "GenerationInterrupted";
    public const string GenerationStarted = "GenerationStarted";
    public const string InactivityTimeoutChanged = "InactivityTimeoutChanged";
    public const string OrphanBlockedPeriodsRemoved = "OrphanBlockedPeriodsRemoved";
    public const string OwnerAccountCreated = "OwnerAccountCreated";
    public const string PasswordChanged = "PasswordChanged";
    public const string RecoveryCodeRegenerated = "RecoveryCodeRegenerated";
    public const string ResourceArchived = "ResourceArchived";
    public const string ResourceCreated = "ResourceCreated";
    public const string ResourceDeleted = "ResourceDeleted";
    public const string ResourceRestored = "ResourceRestored";
    public const string ResourceUpdated = "ResourceUpdated";
    public const string SchedulingProfileDefaultsRestored = "SchedulingProfileDefaultsRestored";
    public const string SchedulingProfileUpdated = "SchedulingProfileUpdated";
    public const string SchoolAssetRemoved = "SchoolAssetRemoved";
    public const string SchoolAssetUploaded = "SchoolAssetUploaded";
    public const string SchoolProfileUpdated = "SchoolProfileUpdated";
    public const string SectionArchived = "SectionArchived";
    public const string SectionCreated = "SectionCreated";
    public const string SectionDeleted = "SectionDeleted";
    public const string SectionRestored = "SectionRestored";
    public const string SectionUpdated = "SectionUpdated";
    public const string SectionsAdded = "SectionsAdded";
    public const string SectionsRemoved = "SectionsRemoved";
    public const string SessionPlanUpdated = "SessionPlanUpdated";
    public const string SetupFinished = "SetupFinished";
    public const string SetupProgressSaved = "SetupProgressSaved";
    public const string ShiftCreated = "ShiftCreated";
    public const string ShiftDayLessonsUpdated = "ShiftDayLessonsUpdated";
    public const string ShiftDeleted = "ShiftDeleted";
    public const string ShiftModeChanged = "ShiftModeChanged";
    public const string ShiftPeriodsUpdated = "ShiftPeriodsUpdated";
    public const string ShiftUpdated = "ShiftUpdated";
    public const string StageArchived = "StageArchived";
    public const string StageCreated = "StageCreated";
    public const string StageDayLessonsLowered = "StageDayLessonsLowered";
    public const string StageDayLessonsSuggested = "StageDayLessonsSuggested";
    public const string StageDayLessonsUpdated = "StageDayLessonsUpdated";
    public const string StageDeleted = "StageDeleted";
    public const string StageRestored = "StageRestored";
    public const string StageUpdated = "StageUpdated";
    public const string SubjectArchived = "SubjectArchived";
    public const string SubjectCreated = "SubjectCreated";
    public const string SubjectDeleted = "SubjectDeleted";
    public const string SubjectRestored = "SubjectRestored";
    public const string SubjectUpdated = "SubjectUpdated";
    public const string SuggestedCurriculumApplied = "SuggestedCurriculumApplied";
    public const string SuggestedCurriculumStageReset = "SuggestedCurriculumStageReset";
    public const string TeacherArchived = "TeacherArchived";
    public const string TeacherCreated = "TeacherCreated";
    public const string TeacherDeleted = "TeacherDeleted";
    public const string TeacherRestored = "TeacherRestored";
    public const string TeacherSpecializationAdded = "TeacherSpecializationAdded";
    public const string TeacherUpdated = "TeacherUpdated";
    public const string TeachersBulkCreated = "TeachersBulkCreated";
    public const string TermCreated = "TermCreated";
    public const string TermDeleted = "TermDeleted";
    public const string TermMadeCurrent = "TermMadeCurrent";
    public const string TermUpdated = "TermUpdated";
    public const string TimetableApproved = "TimetableApproved";
    public const string TimetableArchived = "TimetableArchived";
    public const string TimetableEdited = "TimetableEdited";
    public const string TimetableGenerated = "TimetableGenerated";
    public const string TimetableRolledBack = "TimetableRolledBack";
    public const string WorkingWeekUpdated = "WorkingWeekUpdated";
    public const string WorkloadArchivedWithLine = "WorkloadArchivedWithLine";
    public const string WorkloadAssigned = "WorkloadAssigned";
    public const string WorkloadAssignedAcrossStage = "WorkloadAssignedAcrossStage";
    public const string WorkloadAssignmentsSuggested = "WorkloadAssignmentsSuggested";
    public const string WorkloadClassTeacher = "WorkloadClassTeacher";
    public const string WorkloadCleared = "WorkloadCleared";
    public const string WorkloadReassigned = "WorkloadReassigned";
    public const string WorkloadRemoved = "WorkloadRemoved";
    public const string WorkloadTransferred = "WorkloadTransferred";

    public static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>
    {
        [AcademicYearCreated] = AuditCategories.School,
        [AcademicYearDeleted] = AuditCategories.School,
        [AcademicYearMadeCurrent] = AuditCategories.School,
        [AcademicYearUpdated] = AuditCategories.School,
        [BackupCreated] = AuditCategories.Backup,
        [BackupRestored] = AuditCategories.Backup,
        [BellSettingsUpdated] = AuditCategories.School,
        [CalendarDayCreated] = AuditCategories.School,
        [CalendarDayDeleted] = AuditCategories.School,
        [CalendarDayUpdated] = AuditCategories.School,
        [CurriculumCopied] = AuditCategories.School,
        [CurriculumEntryArchived] = AuditCategories.School,
        [CurriculumEntryCleared] = AuditCategories.School,
        [CurriculumEntryCreated] = AuditCategories.School,
        [CurriculumEntryDeleted] = AuditCategories.School,
        [CurriculumEntryRestored] = AuditCategories.School,
        [CurriculumEntryUpdated] = AuditCategories.School,
        [CurriculumLessonsSet] = AuditCategories.School,
        [GenerationCancelled] = AuditCategories.Generation,
        [GenerationFinished] = AuditCategories.Generation,
        [GenerationInterrupted] = AuditCategories.Generation,
        [GenerationStarted] = AuditCategories.Generation,
        [InactivityTimeoutChanged] = AuditCategories.Settings,
        [OrphanBlockedPeriodsRemoved] = AuditCategories.School,
        [OwnerAccountCreated] = AuditCategories.Account,
        [PasswordChanged] = AuditCategories.Account,
        [RecoveryCodeRegenerated] = AuditCategories.Account,
        [ResourceArchived] = AuditCategories.School,
        [ResourceCreated] = AuditCategories.School,
        [ResourceDeleted] = AuditCategories.School,
        [ResourceRestored] = AuditCategories.School,
        [ResourceUpdated] = AuditCategories.School,
        [SchedulingProfileDefaultsRestored] = AuditCategories.Settings,
        [SchedulingProfileUpdated] = AuditCategories.Settings,
        [SchoolAssetRemoved] = AuditCategories.School,
        [SchoolAssetUploaded] = AuditCategories.School,
        [SchoolProfileUpdated] = AuditCategories.School,
        [SectionArchived] = AuditCategories.School,
        [SectionCreated] = AuditCategories.School,
        [SectionDeleted] = AuditCategories.School,
        [SectionRestored] = AuditCategories.School,
        [SectionUpdated] = AuditCategories.School,
        [SectionsAdded] = AuditCategories.School,
        [SectionsRemoved] = AuditCategories.School,
        [SessionPlanUpdated] = AuditCategories.School,
        [SetupFinished] = AuditCategories.School,
        [SetupProgressSaved] = AuditCategories.School,
        [ShiftCreated] = AuditCategories.School,
        [ShiftDayLessonsUpdated] = AuditCategories.School,
        [ShiftDeleted] = AuditCategories.School,
        [ShiftModeChanged] = AuditCategories.School,
        [ShiftPeriodsUpdated] = AuditCategories.School,
        [ShiftUpdated] = AuditCategories.School,
        [StageArchived] = AuditCategories.School,
        [StageCreated] = AuditCategories.School,
        [StageDayLessonsLowered] = AuditCategories.School,
        [StageDayLessonsSuggested] = AuditCategories.School,
        [StageDayLessonsUpdated] = AuditCategories.School,
        [StageDeleted] = AuditCategories.School,
        [StageRestored] = AuditCategories.School,
        [StageUpdated] = AuditCategories.School,
        [SubjectArchived] = AuditCategories.School,
        [SubjectCreated] = AuditCategories.School,
        [SubjectDeleted] = AuditCategories.School,
        [SubjectRestored] = AuditCategories.School,
        [SubjectUpdated] = AuditCategories.School,
        [SuggestedCurriculumApplied] = AuditCategories.School,
        [SuggestedCurriculumStageReset] = AuditCategories.School,
        [TeacherArchived] = AuditCategories.School,
        [TeacherCreated] = AuditCategories.School,
        [TeacherDeleted] = AuditCategories.School,
        [TeacherRestored] = AuditCategories.School,
        [TeacherSpecializationAdded] = AuditCategories.School,
        [TeacherUpdated] = AuditCategories.School,
        [TeachersBulkCreated] = AuditCategories.School,
        [TermCreated] = AuditCategories.School,
        [TermDeleted] = AuditCategories.School,
        [TermMadeCurrent] = AuditCategories.School,
        [TermUpdated] = AuditCategories.School,
        [TimetableApproved] = AuditCategories.Timetable,
        [TimetableArchived] = AuditCategories.Timetable,
        [TimetableEdited] = AuditCategories.Timetable,
        [TimetableGenerated] = AuditCategories.Timetable,
        [TimetableRolledBack] = AuditCategories.Timetable,
        [WorkingWeekUpdated] = AuditCategories.School,
        [WorkloadArchivedWithLine] = AuditCategories.School,
        [WorkloadAssigned] = AuditCategories.School,
        [WorkloadAssignedAcrossStage] = AuditCategories.School,
        [WorkloadAssignmentsSuggested] = AuditCategories.School,
        [WorkloadClassTeacher] = AuditCategories.School,
        [WorkloadCleared] = AuditCategories.School,
        [WorkloadReassigned] = AuditCategories.School,
        [WorkloadRemoved] = AuditCategories.School,
        [WorkloadTransferred] = AuditCategories.School,
    };

    public static IReadOnlyCollection<string> All => Categories.Keys.ToArray();

    /// <summary>The category of an event type; entries written by an older build keep the school category.</summary>
    public static string CategoryOf(string eventType) => Categories.TryGetValue(eventType, out var category) ? category : AuditCategories.School;
}
