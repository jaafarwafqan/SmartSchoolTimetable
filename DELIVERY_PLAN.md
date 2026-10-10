# Delivery Plan

Scope: one local school, one workstation, exactly one owner account, no external network service. Kestrel binds only to `127.0.0.1`. Arabic-first RTL, offline.

This file describes what is **delivered**, what **remains**, and in what order. Per-phase detail lives in `docs/PHASE*_REPORT.md`, `CHANGELOG.md`, the ADRs in `adr/`, and decisions in `docs/DECISIONS_PENDING.md` (open) and `docs/DECISIONS_LOG.md` (closed). Risks are in `RISKS.md`.

## Delivered

| Phase | Delivered | Tag | Where to read |
|---|---|---|---|
| 0 | Documentation and approval gate, solver and PDF spikes (the spikes were removed afterwards) | `phase-0` | `adr/`, `RISKS.md` |
| 1 | Local foundation: ASP.NET Core + SQLite/WAL, single owner, recovery code, strict Host/Origin/token protection, local audit rows, Arabic error contract, design system | `phase-1.4` | `docs/PHASE1_ACCEPTANCE.md` |
| 2 | School setup: profile, years and semesters, shifts and periods, stages and sections, subjects, teachers, calendar | `phase-2-final` | `docs/PHASE2_REPORT.md` |
| 2.5 | Simpler setup: no drawers, templates, curriculum table, setup wizard, owner's suggested curriculum | `phase-2-5-final`, `phase-2-5-curriculum` | `docs/PHASE25_REPORT.md` |
| 3 | Workload and capacity: resources, scheduling profile, assignments, pre-solve validator and readiness report | `phase-3-final` | `docs/PHASE3_REPORT.md` |
| 4 | Generation and the timetable lifecycle basics (below) | `phase-4i` | `docs/PHASE4_REPORT.md`, `docs/PHASE4G_REPORT.md` |

### What Phase 4 delivered
- **Generation:** Google OR-Tools CP-SAT in process, hard rules H1–H10 and soft rules S1–S5, real progress, cancellation that keeps the best timetable, a deterministic mode, an independent verifier, and infeasibility diagnostics (ADR 0037–0040).
- **Versioning:** every run and every edit saves a new timetable version with its own input snapshot and hash; a version whose input changed is shown as out of date.
- **Manual edit:** move or swap lessons, checked at once by the verifier, undo/redo within the session, saved as a new version linked to its parent.
- **Approval:** one approved version per year.
- **Print:** an A4 print stylesheet (master landscape, section/teacher portrait, RTL); the owner uses "Save as PDF" from the print dialog.
- **Excel export:** one workbook with the master sheet and a sheet per section and per teacher (ADR 0041).
- **Backup and restore:** consistent copy with SQLite's backup, restore with two confirmations and an automatic pre-restore copy (ADR 0042).
- **Release folder:** `tools/Publish-Release.ps1` builds a self-contained win-x64 folder.
- **R1 12-hour time**, **R2 flexible breaks**, **R3 double-shift sessions** with a day→session mapping per semester (ADR 0043).

## Remaining, in order

| Milestone | Scope |
|---|---|
| **M0 Truth and hygiene** | This plan, decisions split, superseded documents archived, `RISKS.md`, CI, naming rule |
| **M1 Timetable lifecycle completion and audit history** | Compare two versions, rollback as a new version, immutable approved/archived versions, undo/redo and lock-or-discard on regenerate, paged audit history and its viewer |
| **M2 Data portability, backups, account, preferences** | Excel import, backup list with integrity check and automatic backup, safe upgrade, change username, stored preferences, support log, PDF decision |
| **M4 Professional UI/UX** | a: icons everywhere (done, `work/phase-5`); b: dashboard; c: split settings; d: UX polish pass |
| **M3 Release validation** | Published folder on a temporary database, localhost-attack suite, offline proof, SQLCipher and WebView2 GO/NO-GO, performance gate |
| **Final user guide** | Reproducible Arabic PDF guide from a scripted scenario |

Order: M0 → M1 → M2 → M4 (b, c, d) → M3 → final user guide. Gates for every milestone: build with 0 warnings, `dotnet test`, ESLint/stylelint, Vitest, Playwright + axe, the published-exe check on a temporary database, CI green on the branch.

### Owner decision: no attendance, no monitoring
The former **Phase 7 (attendance, monitoring, lesson status)** is cancelled. It is replaced by M4 (professional UI/UX). The dashboard shows only real data (no fabricated numbers). The former **Phase 6** (reporting and data portability) is delivered as Phase 4 (print, Excel, backup) plus M2 (import, backups, preferences, PDF decision). The former **Phase 5** (lifecycle) is partly in Phase 4 and completed by M1. The former **Phase 8** (release validation) is M3.

## Environment
- .NET SDK 9.0.318 and Node.js v24.18.0 on PATH. Docker, PostgreSQL and Redis are not used. SQLite is embedded.

## Process rules
- New work goes on a `work/<name>` branch (never on `master`); the owner merges after acceptance. Tags are `phase-N[x]` and never share a name with a branch.
- Destructive commands (delete, move, overwrite, reset) need the owner's explicit confirmation of the exact paths. Backups are never deleted.
