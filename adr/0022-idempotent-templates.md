# ADR 0022: Templates and helpers are previewed and idempotent

- Status: Accepted (Phase 2.5 spec §4)
- Date: 2026-10-05

## Context
Phase 2.5 ships templates (Iraqi stages per school type, suggested subject names, period and working-day presets) and typing savers (copy a stage's curriculum, set the same lessons across stages). The owner must be able to run them at any time, also on a school that already has data, without fear of losing anything.

## Decision
- **Data-driven:** templates are JSON embedded in the Application assembly (`Setup/Templates/*.json`).
  - Subject templates contain **names only**; no official weekly counts are shipped or invented.
  - A test validates the files: keys resolve, every grade has suggestions, every period preset generates.
- **Every template or helper has a preview route** (`…/preview`) that returns the plan without saving. Each line's action is `create`, `update`, `exists`, `unchanged`, `ambiguous` or `notApplicable`. The UI shows the preview before the apply button appears, and any change of input clears the preview.
- **Idempotent apply:**
  - Existing records are matched (stages by template key or normalized name; subjects by normalized name; curriculum lines by subject + label) and left untouched.
  - Section counts are only raised, never lowered.
  - Nothing is deleted or overwritten, except "set across", which updates the one matching line by design.
  - Applying twice changes nothing the second time.
- **Normal services, one transaction:** the stage and subject templates create records through `StagesSectionsService`, `StageCardsService` and `SubjectsService`, so every validation and audit entry applies. `SetupTransaction` runs the whole apply in one database transaction; the first failure rolls everything back and its error is returned.
- **Ambiguity is never guessed:** "set across" on a stage with two identical lines reports `ambiguous` and skips it.

## Alternatives considered
- **Direct inserts from the template:** faster, but bypasses the rules and the audit trail.
- **Overwriting to match the template:** destroys the owner's edits.

## Consequences
- The wizard (2.5D) reuses these services for its steps.
- How to change: the matching rules live in `SetupTemplatesService.PlanStagesAsync`, `SubjectsAsync` and `CurriculumHelpersService`.
