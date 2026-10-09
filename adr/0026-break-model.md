# ADR 0026: Editable breaks per shift

- Status: Accepted (owner model change M1, approved 2026-10-05)
- Date: 2026-10-05

## Context
The period presets ("استراحة واحدة بعد الحصة الثالثة") fixed the break length silently, and the generator allowed one break only. Break lengths differ between schools, for example between primary and preparatory schools.

## Decision
- **A shift's day is generated from:** first start, lesson minutes, lesson count, **up to three breaks**, and an optional **gap between lessons**.
  - Each break has its own position (after lesson N) and its own duration (5–120 minutes).
  - The gap (0–30 minutes, default 0) is placed between two lessons that are not separated by a break.
  - Validation is in `PeriodGenerator`: position 1..N−1 and unique, at most 3 breaks, gap in range.
- **Presets only fill these values;** everything stays editable. Wizard step 3 and the periods generator use the same `BreaksEditor`. Each shift (morning, evening) has its own settings and preview.
- **Suggested break length per school type** comes from `presets.json` (`breakDefaults`). It is shown as a suggestion («اقتراح، يمكن تعديله»). It is 15 minutes for every type until the owner supplies real values; no official numbers are claimed.
- **Saved periods are ordinary rows,** so a gap is simply time between two lesson rows. Reading a shift back (wizard resume) recovers each break's duration and the gap.

## Consequences
- The API generator command and the wizard timing step accept `breaks` and `gapMinutes`.
- How to change: limits in `PeriodGenerator` (`MaxBreaks`, `MaxGapMinutes`); defaults in `Setup/Templates/presets.json`.

## Amendment R2 (2026-10-09, owner requirement)
- No cap on the number of breaks: a break may follow any lesson 1..N−1, at most one per gap. `PeriodGenerator.MaxBreaks` and the `Breaks` count check were removed.
- Durations: 1–120 minutes in the domain (`MinBreakMinutes` was 5, now 1). The editor offers 1–60 with a stepper and quick picks of 5/10/15/20/30 minutes; the old duration list `[5, 10, 15, 20, 25, 30, 40, 45, 60]` is gone.
- `Shift.MaxRows` is `2 × MaxLessons − 1` (23, was 20), so a break can follow every lesson but the last.
- The editor shows every break with its 12-hour clock time. A break after the last lesson or a second break in one gap is shown with an Arabic message and blocks «التالي» and «إنشاء القائمة»; it is no longer dropped silently.
- Old saved periods and the presets give exactly the same rows (`FlexibleBreaksTests.OldPlansGiveExactlyTheSameRows`). No schema change, so no migration.
