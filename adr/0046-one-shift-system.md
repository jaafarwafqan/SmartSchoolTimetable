# ADR 0046: One shift system (morning / evening / double), one timing component

- Status: Accepted (MF1, MF6, MF7, branch `work/phase-5`). Reverses DECISIONS #82 (the old two-shift mode is gone).
- Date: 2026-10-10

## Context
After R3 the school had two overlapping ways to say how the day works: the old «ورديتان بشعب مختلفة» study type (two shifts, each with its own sections) and the new daily-sessions card for «دوام مزدوج». The owner's testing showed the confusion: the sessions card was "unavailable" in the old mode, so no evening times ever appeared; sections and teachers carried a shift field nobody wanted; the timing step and the settings page each had their own editor.

## Decisions
1. **One control, «نظام الدوام»: صباحي / مسائي / مزدوج.** Stored values `morning`, `evening`, `dual`. Sections and teachers never differ by shift; there is exactly one structural shift per year. Endpoints `GET/PUT /shift-system` (and `POST /shift-system/convert`) replace `/shift-mode`.
2. **Double shift = one shift + a session plan** (ADR 0043): the structural shift's periods are the morning timing, the evening timing and the day→session mapping per semester live in `SessionPlan`. A single-session school has no plan.
3. **One merged component** (`ShiftSystemEditor`) is used by the wizard's «الدوام» step and by «المدرسة › الدوام والحصص والجرس». The info box is removed.
4. **Timing templates** carry a `session` (morning/evening) in `presets.json` and are filtered by it. «بدون قالب (أبدأ من الصفر)» is the first and default option (no breaks).
5. **School type is chosen once** (the school profile / wizard step 1). The stage template shows it read-only with a link; there is no per-grade shift selector, and no shift field on sections or teachers.
6. **Legacy data is converted, never lost.** A database still in the old layout gets a one-time Arabic notice and a guided conversion: an automatic `pre-conversion-*.db` backup first, then sections move to the main shift and the other shift is removed (for `dual` the session plan is built from the domain). Until converted, the API answers `SHIFT_SYSTEM_LEGACY` (409). Audit event `ShiftSystemConverted`.

## Consequences
- The files of the old mode (`ShiftModeService`, `ShiftModeCard`, `shiftModeApi`, `SessionsCard`, `ShiftDialog`) were removed after the owner approved those exact paths.
- Wizard: `WizardTimingCommand(Days, WeekStartDay, System, Main, Evening, SessionDays)`; `WizardSchoolCommand.ShiftMode` is optional.
- The earlier "evening times never appear" report came from the old mode making the sessions card unavailable; in the new model the viewer, print and Excel show the evening clock per semester (e2e `phase4-sessions`).
