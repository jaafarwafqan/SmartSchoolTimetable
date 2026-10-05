# Phase 3 progress

Updated after every checkpoint (owner's Phase 3 prompt §0.3).

| Item | State |
|---|---|
| Branch | `phase-3-finish` (from tag `phase-3-as-received` = `cbed0ce`, which holds the uncommitted 3D/3E work received from another agent; 3A–3C are on `phase-3`; nothing pushed or merged) |
| Last green tag | `phase-3c` |
| In progress | C verification (mutation checks, soundness, hash, migration, API, UI review), then D (3E) |
| Next | Tag `phase-3d` once 3D is green, then finish 3E (`phase-3e`, `phase-3-final`) |

## Checkpoints
| Checkpoint | Tag | State |
|---|---|---|
| 3A hardening (§5) | `phase-3a` | done |
| 3B specializations, resources, profile | `phase-3b` | done |
| 3C workload assignments | `phase-3c` | done |
| 3D scheduling input, hash, validator, readiness | `phase-3d` | B1–B5 fixed and green (`e17fb29`…`e7d9926`); C verification in progress |
| 3E suggester, wizard step, demo data, E2E, docs | `phase-3e`, `phase-3-final` | partly written (suggester, wizard step, readiness E2E); demo data, scenarios, owner script and report missing |

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

## 3C results
- `dotnet build -c Release --no-incremental`: 0 warnings. `dotnet test -c Release`: 173 passed.
- `npm run lint`: clean. `npm test`: Vitest 83, Playwright 15 (new `phase3-workload.spec.ts`; screenshots viewed before accepting).

## Phase 3 finish: B1–B5 (on `phase-3-finish`)
- B1 `e17fb29` (finding-code contract), B3 `66a11e0` (wizard locators), B5 `be0500b` (workload baseline), B4 `c787a79` (checklist test, zero counts, readiness de-flake), C3 `848cb2c` (double-period mode), B2/D3 `e7d9926` (demo data).
- After them: Release build 0 warnings; .NET 202 passed; lint clean; Vitest 90 passed; Playwright 17 passed.

## Known problems and notes
- The `/design` style guide does not yet show the matrix, load bar and readiness components (§9); planned for 3E together with the readiness card.
- **A running app instance locked the Release build output** during 3A: `SmartSchoolTimetable.Api.exe` (PID 664), started at 15:22 by `dotnet run --configuration Release --no-build`, probably the owner's own session. It was **not stopped**. Debug builds were used until it exited by itself; the Release checks then ran normally.
- The Playwright suites for reference protection (scenario d), cell undo (e) and orphan blocked periods (f) belong to checkpoint 3E (prompt §6, §8). 3A covers these features with API, service and Vitest tests.
- Kinds with no dependents yet (teacher, section, curriculum line, resource) gain them with resources (3B) and workload assignments (3C).
