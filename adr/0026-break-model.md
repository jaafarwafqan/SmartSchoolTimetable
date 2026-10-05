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
