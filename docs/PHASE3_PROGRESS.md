# Phase 3 progress

Updated after every checkpoint (owner's Phase 3 prompt §0.3).

| Item | State |
|---|---|
| Branch | `phase-3` (from `master` at `6b8d878` "Merge Phase 2.5"; nothing pushed or merged) |
| Last green tag | `phase-3b` |
| In progress | — |
| Next | 3C: workload assignments (model, services, API, «الأنصبة» screens, bulk actions, protections) |

## Checkpoints
| Checkpoint | Tag | State |
|---|---|---|
| 3A hardening (§5) | `phase-3a` | done |
| 3B specializations, resources, profile | `phase-3b` | done |
| 3C workload assignments | `phase-3c` | not started |
| 3D scheduling input, hash, validator, readiness | `phase-3d` | not started |
| 3E suggester, wizard step, demo data, E2E, docs | `phase-3e`, `phase-3-final` | not started |

## 3A results
- `dotnet build -c Release --no-incremental`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 158 passed (150 before Phase 3).
- `npm run lint`: clean (ESLint and Stylelint).
- `npm test`: Vitest 76 passed (73 before), Playwright 13 passed.

## 3B results
- `dotnet build -c Release --no-incremental`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 165 passed.
- `npm run lint`: clean.
- `npm test`: Vitest 78 passed, Playwright 14 passed (new `phase3-resources-profile.spec.ts`; the four settings screenshots were re-baselined for the new tabs after viewing them).

## Known problems and notes
- **A running app instance locked the Release build output** during 3A: `SmartSchoolTimetable.Api.exe` (PID 664), started at 15:22 by `dotnet run --configuration Release --no-build`, probably the owner's own session. It was **not stopped**. Debug builds were used until it exited by itself; the Release checks then ran normally.
- The Playwright suites for reference protection (scenario d), cell undo (e) and orphan blocked periods (f) belong to checkpoint 3E (prompt §6, §8). 3A covers these features with API, service and Vitest tests.
- Kinds with no dependents yet (teacher, section, curriculum line, resource) gain them with resources (3B) and workload assignments (3C).
