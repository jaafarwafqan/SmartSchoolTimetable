# Owner changes review (Phase 2 resume, 2026-10-04)

**Snapshot:** commit `e1e67ec` "Owner manual changes (as received, before review)", on top of `phase-2b` (`1634eb4`).
**What it adds:** checkpoint 2C (stages and sections): Domain, Application, Api, migration, UI page, dashboard steps and counts, and tests.
**Baseline before the review:**
- The build is clean, and 88/88 .NET tests pass.
- Lint is clean, and Vitest passes 41/41.
- Playwright passes 3 of 4: the dashboard "all steps done" assertion fails because new checklist steps were added.

The owner's design is kept. Routes stay year-scoped (`/academic-years/{yearId}/stages/...`), as do the entity shapes, the archive/restore actions, the rule "a stage with active sections cannot be archived", the year-structure copy of stages and sections, and the dashboard counts and steps. The "Fix" column says what was changed and why.

| # | File(s) | Finding | Fix |
|---|---|---|---|
| 1 | `StagesSectionsService.cs` | Name, length, order and student-count rules are duplicated from the Domain (`ValidateStage`, `ValidateSection`). The two copies can drift. | The Domain is the single source; the service maps `DomainValidationException`. |
| 2 | `StagesSectionsService.cs`, `SchoolSetupConfiguration.cs` | A duplicate display order was reported as `DUPLICATE_NAME` on the `Name` field, which is wrong. A unique order also makes reordering need a temporary value. | Display order is no longer unique (ties sort by name). The intent "a clear order" is kept by the default sort. Recorded in DECISIONS_PENDING #10. |
| 3 | `StagesSectionsService.cs` | A unique-index violation on save (`DataConflictException`) returned `CONFLICT`, which tells the owner to reload. | It now returns `DUPLICATE_NAME` on the name or label field. |
| 4 | `StagesSectionsService.ListSectionsAsync` | N+1 queries: for every row it loads the shift and the working week. | One query for the week and one for the shifts on the page. |
| 5 | `StagesSectionsService.ToDtoAsync` | The weekly capacity formula (a business rule) lives in Application. | Moved to `Section.WeeklyCapacity(WorkingWeek, Shift?)` in the Domain, with unit tests. |
| 6 | Service and endpoints | No hard delete. The spec requires "hard delete only for unreferenced records, after a confirmation dialog". | Added `DELETE` for stages (refused with `RECORD_IN_USE` while sections exist) and sections (nothing references them in Phase 2). The UI asks for confirmation. |
| 7 | Service | A section could be restored inside an archived stage, where it would be invisible. | Refused with the new code `STAGE_ARCHIVED` (409, Arabic message added); creating a section in an archived stage now returns it too instead of `NOT_FOUND`. |
| 8 | `YearStructureService.CopyAsync` | Target shifts were matched by `DisplayOrder` and stages by name with `.Single`. A duplicate order or name would throw. | The copy keeps a source-id → copy map, so each section links to the exact copied stage and shift. |
| 9 | `YearStructureService.DeletionBlockAsync` | It checked stages twice (`HasStructureAsync` already includes them). | Simplified. |
| 10 | `Persistence/Migrations/20261003205228_Phase2CStagesSections*` | The migration was generated into a new folder; every other migration lives in `Infrastructure/Migrations/`. | Moved into `Infrastructure/Migrations/`. The namespace and the model snapshot are unchanged. |
| 11 | `SchoolSetupConfiguration.cs` | `Version` concurrency was configured explicitly, duplicating the global convention in `LocalDbContext`. | Removed (the model is unchanged). |
| 12 | Application folder | The spec names an Application feature folder `Stages`; the service was put in `SchoolSetup`. | Moved to `Application/Stages`. The architecture test lists it as a feature. |
| 13 | `StagesSectionsPage.tsx` | Several screen rules were broken. See the list after this table. | Rebuilt as `StagesPanel`, `StageDialog`, `SectionsPanel`, `SectionDialog`, with the owner's labels and layout. |
| 14 | `pages.css` (2C rules) | Uses undefined variables `--color-border`, `--color-text-muted` and `--space-2/3/4`, so the borders and gaps never render. The 2B rules also use undefined `--border-*`, `--surface-raised` and `--ink-*`. | Replaced with the real tokens. A new Vitest test fails on any `var(--x)` that is not defined. |
| 15 | `dashboardItems.ts`, `AppShell.tsx`, `navigation.ts`, `school.ts` | These are correct: Arabic labels, a lucide icon, routes. | Kept. |
| 16 | Tests (`SchoolSetupDomainTests`, `SchoolSetupServiceTests`, `TimetableStructureApiTests`) | The owner added useful cases, but no test covers the section capacity, archive rules, delete, list paging, or the unauthenticated, Origin and token checks for the new routes. | Tests added (see TESTING.md). |
| 17 | `e2e/phase2-school.spec.ts` | Fails because the checklist grew. | The flow now completes the new steps (2B structure, 2C stages and sections). |

Problems found in `StagesSectionsPage.tsx` (finding 13):
- The 77 lines are mostly single lines that hold whole forms.
- There are no `<form>`s, so there is no inline validation and focus never moves to the first invalid field.
- Create errors are silently dropped (no `onError`).
- There is no conflict or reload message.
- Stages and sections cannot be edited.
- Icons lack `aria-hidden`.
- Empty states are bare `<p>` text, not `EmptyState`.

## Problems also found in committed 2B code (fixed in the quality gate)
| # | Area | Finding | Fix |
|---|---|---|---|
| B1 | `scheduleApi.ts` / `tonePreview.ts` | The API sends tones in camelCase (`classic`), but the UI expects `Classic`. The tone select shows nothing, and "test sound" throws, because `patterns["classic"]` is undefined. | The UI uses the API values. |
| B2 | `TimetableStructureService` | Shift name and order rules are duplicated from the Domain. Kind and tone are parsed by hand instead of with `InputErrors.Option`. A duplicate order is reported as `DUPLICATE_NAME`. `DataConflictException` becomes `CONFLICT`. | Same fixes as 1–3. |
| B3 | 2B endpoints | Shifts cannot be deleted. | `DELETE /academic-years/{yearId}/shifts/{id}`, refused with `RECORD_IN_USE` while sections use the shift. |
| B4 | `ScheduleStructurePage.tsx` | The same problems as 13. Also: generator errors are swallowed (`catch { setDraft([]) }`); rows cannot be added, removed or re-kinded; row errors (`Periods[i].*`) are never shown; the week start day cannot be changed; shifts cannot be renamed or deleted. | Split into components with proper forms and row-level errors. |
