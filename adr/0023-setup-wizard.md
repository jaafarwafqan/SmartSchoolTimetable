# ADR 0023: Setup wizard design

- Status: Accepted (Phase 2.5 spec §5)
- Date: 2026-10-05

## Context
A new owner should reach a usable school (year, timing, stages, sections, subjects, curriculum) by choosing rather than typing, in one guided flow. The flow must be resumable, safe to repeat, and must not bypass the rules of the normal screens.

## Decision
- **Seven linear steps** at `/setup`, inside the normal app frame: المدرسة، السنة الدراسية، الدوام، الصفوف والشعب، المواد والمنهج، المعلمون (optional, skippable)، المراجعة.
- **Entry points:**
  - It opens once, right after a *fresh* account's recovery code is confirmed. A one-shot UI flag is set by the setup screen; logins, recoveries and existing databases are never redirected.
  - Afterwards it is reachable from the dashboard card «استكمال الإعداد» (shown until finished) and from Settings.
  - «إكمال لاحقاً» returns to the dashboard at any step.
- **Server steps 1–3** (`PUT /setup-wizard/school|year|timing`) combine several services. Each runs as **one transaction** (`SetupTransaction`) that calls the normal services (`SchoolProfileService`, `AcademicYearService`, `ShiftModeService`, `TimetableStructureService`) and records the step in `SetupProgress`. The first failure rolls everything back and its error code is returned.
  - `EfDataStore.ExecuteInTransactionAsync` now **joins** an already open transaction instead of opening a nested one. Services that use their own transaction (making a year current) take part in the step's transaction.
- **Steps 4–6** reuse the screens' components and endpoints: stage template and stage cards, subject template and the curriculum table, teacher bulk add. "Next" records the step (`PUT /setup-progress`).
- **Idempotent:** running a step again with the same input leaves the data unchanged.
  - Step 1 updates the profile.
  - Step 2 reuses a year with the same label and updates terms with the same names.
  - Step 3 replaces the periods with the same generated list.
  - Steps 4–5 use the idempotent templates (ADR 0022).
- **Resume:** the stored current step is read once when the wizard opens. After that the step is local state, so a step's own save cannot unmount it before its success callback runs. Moving to a step focuses its title.
- **Review** (`GET /setup-wizard/review`) shows real counts and warnings (no sections, empty curriculum, under or over capacity per shift). Warnings never block "finish".

## Alternatives considered
- **A client-only wizard calling the existing endpoints:** no transaction across a step's several writes, so a half-saved step could remain.
- **A separate wizard data model applied at the end:** duplicates the rules and loses the work if the owner stops halfway.
- **Redirecting to the wizard whenever setup is unfinished:** intrusive for an owner who prefers the screens, and it would also trigger on existing databases.

## Consequences
- New wizard steps must use the normal services, and run inside `SetupTransaction` when they write more than once.
- How to change: the step endpoints live in `SetupWizardService`; the UI in `features/setup-wizard`.
