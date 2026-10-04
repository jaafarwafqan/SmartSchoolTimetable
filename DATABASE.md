# Database Design

## Storage model
- One local SQLite database per installation, WAL enabled; no shared database, remote database, tenant key, Redis, or PostgreSQL RLS. See [ADR 0011](./adr/0011-sqlite-storage.md).
- EF Core migrations are the only schema change mechanism.
- Database and backups are local files. Access control relies on the operating-system account and file permissions; baseline SQLite is not encrypted.
- SQLCipher encryption is an optional final-phase decision, recorded in `adr/0010-sqlcipher-go-no-go.md`.
- The canonical database is one `.db` file. WAL mode may create transient `-wal` and `-shm` sidecars while open; never copy the live main database file as a backup.
- Use SQLite Online Backup API/provider equivalent for a consistent single-file backup. A file-copy alternative requires stopping/quiescing database users, checkpointing/truncating WAL, closing connections, copying the main file, and validating the copy with SQLite integrity checks.

## Persistence abstraction
Domain and Application depend on repository/unit-of-work contracts, not SQLite connection types or SQL dialect. EF Core SQLite mappings and database operations live in Infrastructure. This keeps a future PostgreSQL provider possible without committing to or claiming unverified cross-provider compatibility. PostgreSQL would require a new ADR, provider-specific migrations, and compatibility tests.

## Owner account
The local `Users` table stores the single owner account and is intentionally shaped as one account row per identity so additional users could be added later without replacing the identity table. The application enforces exactly one owner account at present; the schema does not include tenant, role, or permission columns.

Account fields (`Users` table, migrations in `src/SmartSchoolTimetable.Infrastructure/Migrations/`):
- `Id` (INTEGER PK); `OwnerSlot` (always `owner`, unique index — enforces the single owner); `Username`; `NormalizedUsername` (unique index);
- `PasswordSalt` (16 bytes), `PasswordHash` (32 bytes), `PasswordIterations` (600,000). The algorithm (PBKDF2-HMAC-SHA-256) is fixed in code; there is no algorithm/version column, so changing algorithms requires an ADR, a version column and a migration;
- `RecoverySalt` (16 bytes) and `RecoveryCodeHash` (32-byte salted SHA-256), both NOT NULL; the hash is replaced (never nulled) on recovery or regeneration;
- `RecoveryCodeAcknowledged` (bool);
- `HasCustomInactivityTimeout` (bool, default false) and `InactivityTimeoutMinutes` (int, nullable) hold the owner's auto-lock choice. Until a choice is made the configured default applies; when `HasCustomInactivityTimeout` is true, a `NULL` value means "never". These columns were added by `20261003175642_AddOwnerInactivityTimeoutPreference`, generated with the local `dotnet-ef` tool (ADR 0015);
- `CreatedAt`, `UpdatedAt`.

The failed-login/lockout columns from the initial migration were removed by `20261003160000_RemoveEscalatingLoginLockoutFields`. `AuditHistory` stores `Id`, `OccurredAt` (indexed), `EventType`, `Target`, `Summary`; current events are `OwnerAccountCreated`, `RecoveryCodeRegenerated`, `PasswordChanged`, and `InactivityTimeoutChanged`. The test `EfModelHasNoPendingChangesVersusTheLatestMigration` fails if the EF model drifts from the latest migration.

First-run setup is permitted only while the account table is empty. It atomically creates the owner and stores only the recovery-code hash. There is no refresh-token table.

## School setup tables (Phase 2)
Every editable table carries `Version` (INTEGER). It starts at 1 and the domain increments it on each change. EF Core treats it as a concurrency token by convention for every `VersionedEntity`. A stale write raises `DbUpdateConcurrencyException`, which `EfDataStore` turns into the 409 `CONFLICT` path. A SQLite constraint violation (error 19) becomes `DataConflictException` (duplicate name or record in use).

Names used for uniqueness and search are stored twice: as typed, and as a `Normalized…` column produced by `ArabicText.Normalize`. Normalization trims and collapses spaces, removes tatweel and diacritics, unifies alef and yaa forms, converts to Western digits and lower-cases.

Migration `20261003183255_Phase2ASchoolProfileAndAcademicYears` was generated with the local `dotnet-ef` tool and includes its Designer file:
- `SchoolProfile`: exactly one row (`Id` = 1, seeded at startup).
  - `Name`, `SchoolType` and `StudyType` (enum strings), `PrincipalName`, `ScheduleOfficerName`.
  - `TimeZoneId` (default `Asia/Baghdad`), `NumeralSystem` (default `ArabicIndic`), `CalendarDisplay` (default `Gregorian`).
  - `UpdatedAt`, `Version`.
  - Nullable owned columns `LogoStoredFileName`, `LogoContentType`, `StampStoredFileName`, `StampContentType`.
- `AcademicYears`: `Label`, `NormalizedLabel` (unique), `StartDate`, `EndDate`, `IsCurrent`, `CurrentTermId`, `Version`. A partial unique index on `"IsCurrent" = 1` allows at most one current year.
- `Terms` (owned by a year, cascade delete): `AcademicYearId`, `Name`, `NormalizedName` (unique per year), `StartDate`, `EndDate`.
- Changing the current year clears the old flag and sets the new one in two saves inside one transaction, so the partial unique index is never violated mid-statement.

Migration `20261003200446_Phase2BTimetableStructure` (local `dotnet-ef`, with Designer file) adds:
- `WorkingWeek` singleton (`Id` = 1): weekday bit mask, week start day, concurrency version. Startup seeds Sunday–Thursday and Sunday week start.
- `BellSettings` singleton (`Id` = 1): built-in tone name, break bell flag, concurrency version. Startup seeds the Classic tone.
- `Shifts`: academic year FK (cascade), display name and normalized name, order and version; unique normalized name per year; display order is indexed but not unique (migration `Phase2CDisplayOrderIndexes`).
- `LessonPeriods` owned by a shift: row position, lesson/break kind, start/end time, per-period start/end bell flags. Position is unique within a shift.
- Period generator output is transient and editable until saved. No calendar days are copied with year structure.

Migration `20261003205228_Phase2CStagesSections` adds:
- `Stages`: academic-year FK (cascade), display and normalized name, display order, archive timestamp and version; normalized name is unique per year; display order is indexed but not unique since `20261004060745_Phase2CDisplayOrderIndexes` (DECISIONS_PENDING #10).
- `Sections`: stage FK and shift FK (both restrictive), display and normalized label, optional student count, archive timestamp and version; normalized label is unique within its stage.
- A section's weekly capacity is computed from the singleton working-week day count and its shift's lesson count, excluding breaks. It is not persisted, so changes in either source appear immediately.

Image files are not stored in the database. Logo and stamp bytes live in `<database folder>/assets/` under generated names matching `^(logo|stamp)-[0-9a-f]{32}\.(png|jpg|webp)$` ([ADR 0016](./adr/0016-school-asset-storage.md)). Backups must copy this folder together with the database.

Migration `Phase2DSubjects` adds:
- `Subjects`:
  - Columns: `Name`, `NormalizedName` (unique), `ColorIndex` (check 1–10), `Priority` (check 1–5), `DistributionEnabled`, `SpreadAcrossDays`, `Heavy`, `RequiresDoublePeriod`, `Notes` (≤ 500), `IsArchived` (indexed), `ArchivedAt`, `Version`.
  - Subjects are global, not year-scoped.
- `SubjectBlockedPeriods` (owned by a subject, cascade delete): `SubjectId`, `Day` (ISO weekday), `LessonNumber`, unique per subject.

Migration `Phase2ETeachers` adds:
- `Teachers`:
  - Names: `FullName`, `NormalizedFullName` (indexed), `ShortName`, `NormalizedShortName` (unique).
  - Constraints: `OffDaysMask` (bit day-1), `FullyReleased`, `ReleaseReason`, `ReleaseFrom`, `ReleaseTo`, `MaxLessonsPerDay`, `MaxLessonsPerWeek` (nullable).
  - Also `Notes`, `IsArchived` (indexed), `ArchivedAt`, `Version`.
- `TeacherBlockedPeriods` (owned by a teacher, cascade delete): `TeacherId`, `Day`, `LessonNumber`, unique per teacher.

Migration `Phase2FCalendar` adds `CalendarDays`:
- `Title`, `NormalizedTitle`, `StartDate`, `EndDate` (check `EndDate >= StartDate`, indexed together), `Kind` (enum string), `AffectsSchedule`, `Version`.

Demo databases created with `--seed-demo-data` use exactly this schema (migrated on creation) in a separate file.

Migration `Phase25BDayLessonsShiftModeSetup` (2.5B) adds:
- `Shifts.Kind` (`Morning`, `Evening` or `Other`; existing rows get `Other`).
- `ShiftDayLessons`: `ShiftId`, `Day`, `Lessons`, unique per shift and day. Only days that differ from the shift's lesson count are stored (ADR 0020).
- `SetupProgress`: one row (`Id` = 1, seeded at startup) with `CurrentStep`, `CompletedMask`, `SkippedMask`, `IsFinished`, `UpdatedAt`, `Version`.

Migration `Phase25CCurriculumTemplates` (2.5C) adds:
- `Stages.TemplateKey` (≤ 40, nullable): the template grade and branch a stage came from.
- `CurriculumEntries`: `StageId`, `SubjectId` (both `Restrict`), `WeeklyLessons` (check 1–15), `Label`, `NormalizedLabel`, `NeedsDoublePeriod`, `Notes`, `IsArchived`, `ArchivedAt`, `Version`. Indexed on (StageId, SubjectId) and SubjectId; deliberately **not unique** (ADR 0021).

## Application data
- School profile; teachers, subjects, resources, stages, sections, workload, shifts, bell times, calendar
- Timetable versions and lessons
- Local audit history for version changes, publish, rollback, backup/restore, password changes, and imports
- Any future application tables use local stable identifiers and explicit foreign keys; no `TenantId`

## Connection settings
- `Foreign Keys=True`, `Default Timeout=30`, `Pooling=False` in the connection string (`LocalInfrastructureRegistration`).
- `PRAGMA journal_mode=WAL` is set once at startup and persists in the database file.
- `PRAGMA synchronous=FULL` is connection-scoped, so `SqlitePragmaInterceptor` applies it every time EF Core opens a connection (tested by `EveryEfCoreConnectionAppliesSynchronousFull`).

## Integrity and concurrency
- Current tables use primary keys and unique indexes only (no relations exist yet). Future application tables must add foreign keys, unique and check constraints, and optimistic version columns where they protect local data.
- A local single-user process does not need cross-tenant isolation or distributed concurrency coordination.
- UI and application services prevent conflicting local edits and concurrent generation jobs.
- Backups/restores operate on the local database and files and must be validated before replacement. Until the Phase 6 in-app backup exists, the README documents an interim stopped-app file copy.
