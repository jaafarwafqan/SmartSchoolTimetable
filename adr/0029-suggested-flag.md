# ADR 0029: Suggested curriculum lines carry an `IsSuggested` flag

- Status: Accepted (owner instruction, 2026-10-05)
- Date: 2026-10-05

## Context
Applying the suggested curriculum must be idempotent and must never overwrite a value the owner typed. The owner also needs to see which values are still suggestions.

## Decision
- **`CurriculumEntry.IsSuggested`** (migration `Phase25SuggestedCurriculum`, default false):
  - `CreateSuggested` sets it;
  - any owner edit (`Update`, `SetWeeklyLessons`, the cell editor, "set across") clears it;
  - `CopyTo` keeps it.
  - The table shows a small «مقترح» mark under such cells.
- **Apply only adds:** missing subjects, and missing (stage, subject) lines. A stage that already has any line for the subject keeps it unchanged. Nothing is deleted. Running it twice changes nothing.
- **"إعادة المقترح لهذه المرحلة"** is the only way to bring a value back to the suggestion.
  - It shows a before/after preview and needs the owner's confirmation (`confirm: true` in the API; refused with `Confirm` `REQUIRED` otherwise).
  - It updates or creates the stage's main line per subject and marks it suggested again. Extra lines the owner added are kept.
- **Subjects created by the template** get the automatic palette colour, priority 3 and no advanced flags (same as quick add).
- **Every apply and reset** runs in one transaction (ADR 0023) and is audited (`SuggestedCurriculumApplied`, `SuggestedCurriculumStageReset`).

## Consequences
- Phase 3 can treat suggested lines like any other line; the flag only informs the owner.
