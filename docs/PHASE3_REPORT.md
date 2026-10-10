# Phase 3 report: workload, resources, scheduling profile, pre-solve validation

Branch `phase-3-finish`, created from tag `phase-3-as-received` (`cbed0ce`, the uncommitted 3D/3E work received from another agent). Nothing was pushed or merged; the owner does both. Phase 4 was not started.

## Checkpoints and tags
| Checkpoint | Tag | Commit |
|---|---|---|
| 3A hardening | `phase-3a` | `9eff752` |
| 3B resources, specializations, profile | `phase-3b` | `406aa15` |
| 3C workload assignments | `phase-3c` | `c19e45b` |
| 3D scheduling input, hash, validator, readiness (B1–B5 fixes, C verification) | `phase-3d` | `cc49ec3` |
| 3E suggester, wizard step, demo data, scenarios, docs | `phase-3e`, `phase-3-final` | the commit of this report |

`phase-3d` is on `cc49ec3` and not on `8cb164b`. At `8cb164b` the readiness screenshot still showed the unmasked check time, so the Playwright suite was not reliably green. `cc49ec3` also contains the D1 scenarios.

Phase 3 finish commits: `e17fb29` B1, `66a11e0` B3, `be0500b` B5, `c787a79` B4, `848cb2c` C3, `e7d9926` B2/D3, `8cb164b` C1/C2/C4/C5/C6, `cc49ec3` C7/D1, `e5cf6f1` D2, then the docs and report commit.

## Verification (final run, from the repository root)
| Command | Result |
|---|---|
| `dotnet build .\SmartSchoolTimetable.sln --configuration Release --no-incremental` | Build succeeded, 0 warnings, 0 errors |
| `dotnet test .\SmartSchoolTimetable.sln --configuration Release --no-build` | Passed 214, failed 0, skipped 0 |
| `npm.cmd --prefix .\frontend run lint` | clean (ESLint and Stylelint, exit 0) |
| `npm.cmd --prefix .\frontend test` | Vitest 25 files, 91 tests passed; Playwright 21 passed (exit 0) |
| Line coverage (coverlet, Cobertura) | Domain 97.3%, Application 95.4% (target ≥ 90%) |

## Proof per requirement
### B. Known failures
| Item | Fix | Proof |
|---|---|---|
| B1 finding codes vs `ErrorContractTests` | `FindingCodes.All` registry; `FindingCodes.cs` is a definition site like `ErrorCodes.cs`; the guard logic is unchanged | `FindingCodeContractTests.EveryConstantIsInTheRegistryExactlyOnce`, `.EveryCodeHasAnArabicMessageAndAUnionEntryInTheFrontend`, `.NoFindingCodeLiteralAppearsOutsideItsDefinition`; Vitest `readinessPresentation.test.ts` "falls back to a generic Arabic message for an unknown code"; `ErrorContractTests` passes |
| B2 demo checklist | Demo data extended (D3); the test still asserts every checklist item is done | `CalendarAndDemoDataTests.DemoDataCreatesASeparateCompleteSampleSchool` |
| B3 wizard strict-mode locator | Exact step-title locator in the shared helper; no UI text renamed | `phase25-wizard`, `phase25-scenarios`, `phase25-fixes` pass |
| B4 Phase 2 checklist | The test completes the workload step through the real UI and keeps the final assertion | `phase2-school.spec.ts` "the whole setup checklist completes end to end" |
| B5 matrix screenshot at 375 px | The diff was an intended change (new content), not a layout regression; re-baselined after viewing (`be0500b`) | `phase3-workload.spec.ts` |

### C. Verification of the 3D code
| Item | Proof |
|---|---|
| C1 mutation checks | Table below; 18/18 caught after the added tests |
| C2 soundness, 300+ cases | `ValidatorPropertyTests.ARandomValidTimetableGivesNoError` and `.LoweringOneTeachersLimitBelowTheirLoadReportsExactlyThatTeacher` (`Runs = 300` each, resources, double periods, two shifts, per-stage day counts that differ between days; `TheGeneratorProducesRealSchools` checks that the generator covers them). Exact examples: `PreSolveValidatorTests.TeacherNeeding28WithOnly25AvailableIsShort3`, `.PhysicsNeeding7WithOnly5AllowedIsShort2` |
| C3 double-period severity | `ValidatorOptions { DoublePeriodsRequired }`, default false; readiness `?doublePeriods=true`; checkbox «فحص وضع الدروس المزدوجة». `PreSolveValidatorTests.DoublePeriodsNeedConsecutivePairs` (both modes), `ReadinessTests.TheDoubleLessonModeTurnsAnImpossibleDoubleIntoAnError`, Vitest double-period note; ADR 0035 and DECISIONS_PENDING #65 |
| C4 hash | `SchedulingInputHashTests.ReadingOrderAndNamesDoNotChangeTheHash`, `.ResourceCapacityAndAssignmentsChangeTheHash`, `PreSolveValidatorTests.TheHashIsStableAndChangesWithEveryRelevantValue` (lesson count, blocked period, teacher limit, profile weight) |
| C5 data migration | `WizardStepMigrationTests.AFinishedWizardKeepsItsReviewAsStepEight`, `.AWizardInTheMiddleIsNotTouched`, `.ADatabaseWithoutProgressStaysEmpty`, `.DownRestoresTheSevenStepMasks`, `.DownDoesNotTurnADoneWorkloadStepIntoADoneReview`. Bug found and fixed: `Down` cleared only bit 256, so a done workload step came back as a done review |
| C6 API | `ReadinessTests.ReadinessRequiresAuthenticationAndUsesThePersistedWorkload` (401), `.ReadinessAndSuggestionsFollowTheLocalRequestRules` (foreign Origin 403, foreign Host 400, apply without the launch token 403), `QueryCountTests` (readiness), `.SqliteSnapshotHashAndValidationMeetTheFortySectionBudget` |
| C7 UI review | Screens opened through Playwright on temporary databases and viewed. Defects fixed in `cc49ec3`: bulk actions could pick a section of the previously chosen stage while the new stage loaded; a group's shortage was a bare number (now «النقص: …»); the suggester `h2` inside the wizard step's `h2` (now `h3`); «خطوط» / «حتمي» wording (now «بنود المنهج» / «ثابت»); the "wizard-step-7" screenshot showed the review step (now step 7 and step 8 each have their own); the readiness check time and hash made screenshots unstable (masked) |

### D. 3E
| Item | Proof |
|---|---|
| D1 (a) primary, bulk class teacher → ready | `phase3-scenarios.spec.ts` "(a) primary: twelve class teachers assigned with the bulk action, then readiness is ready" |
| D1 (b) 28/25 shortage, Arabic message, deep-link fix | `phase3-readiness.spec.ts` "(3D) exact 28/25 teacher shortage opens workload and is fixed from the deep link" |
| D1 (c) resource shortage | `phase3-scenarios.spec.ts` "(c) resource shortage and (d) protection of subject, section and stage" |
| D1 (d) reference protection | same test (subject, section, stage); teacher in `phase3-workload.spec.ts` |
| D1 (e) undo of clearing a curriculum cell | `phase3-workload.spec.ts` (clearing an assigned line, confirmation, «تراجع عن الإفراغ») |
| D1 (f) orphan blocked periods | `phase3-scenarios.spec.ts` "(f) orphan blocked periods after lessons per day shrink" |
| D1 (g) readiness screen and dashboard card | `phase3-readiness.spec.ts` "(3D) dashboard and readiness report show the real unassigned-line finding" |
| D1 (h) suggester: preview = applied, existing kept | `phase3-scenarios.spec.ts` "(h) intermediate: the suggester assigns twelve sections…"; `AssignmentSuggesterTests` |
| D1 layout | axe, no horizontal page scroll and no text overlap at 375/768/1024/1440 in every scenario; screenshots viewed before acceptance |
| D2 `/design` | `SchedulingSection` shows the matrix, load bar and readiness finding; `SchedulingSection.test.tsx` renders them (the route is development-only, so Playwright on the production build cannot open it) |
| D3 demo data | About 20 teachers with specializations and constraints, sports field capacity 2, computer lab capacity 1, assignments made by the suggester, every number marked as a sample. `CalendarAndDemoDataTests.DemoDataCreatesASeparateCompleteSampleSchool` (all checklist items done, ready), `.DemoDataWithProblemsShowsTheThreeReadinessErrors` (`--with-problems`: overloaded teacher short 3, physics slots, field capacity), `.DemoTargetRefusesMissingProtectedAndExistingPaths` |
| D4 UX metric | Below |
| D5 documents | DOMAIN, DATABASE, API, DESIGN_SYSTEM, DELIVERY_PLAN, TESTING, README, CHANGELOG (3D and 3E entries), ADR 0033–0035 checked against the code, `docs/OWNER_TEST_SCRIPT_PHASE3.md` (31 Arabic steps, each with «المتوقع») |

## Mutation checks (C1)
`docs/phase3-mutation-checks.py` changes one line at a time in the working copy, runs the relevant tests and always writes the original back (never committed).

| Mutant | First run | Test added |
|---|---|---|
| M01 shortage arithmetic | caught | — |
| M02 section over capacity `>` → `>=` | caught | — |
| M03 section under capacity `<` → `<=` | caught | — |
| M04 teacher overload `>` → `>=` | caught | — |
| M05 subject allowed slots ignore blocks | caught | — |
| M06 resource capacity ignored | caught | — |
| M07 double pairs counted twice | caught | — |
| M08 unassigned lines never reported | caught | — |
| M09 availability ignores off days | caught | — |
| M10 availability ignores blocked periods | caught | — |
| M11 availability ignores max per day | caught | — |
| M12 availability ignores max per week | caught | — |
| M13 hash: teachers not sorted | **survived** | `SchedulingInputHashTests.ReadingOrderAndNamesDoNotChangeTheHash` |
| M14 hash: `NotHashed` ignored | caught | — |
| M15 suggester ignores the teacher limit | **survived** | `AssignmentSuggesterTests.TheSuggesterNeverGoesAboveATeachersLimit` |
| M16 migration mask shift wrong | **survived** | `WizardStepMigrationTests` (five tests) |
| M17 readiness endpoint without session | caught | — |
| M18 migration `Down` keeps the workload bit | added with the bug fix | `WizardStepMigrationTests.DownDoesNotTurnADoneWorkloadStepIntoADoneReview` |

After the added tests, all 18 mutants are caught (commit `8cb164b`).

## Performance
`ReadinessTests.SqliteSnapshotHashAndValidationMeetTheFortySectionBudget` (40 sections × 9 subjects: SQLite snapshot, hash and validation, budget 1 s): 38, 44 and 40 ms in three consecutive runs.

## UX metric (D4)
Counted by `UxMeter` in `phase3-scenarios.spec.ts` (typed = values written; chosen = values selected; commands = clicks and key presses that act).

| School | Method | Typed | Chosen | Commands |
|---|---|---|---|---|
| Primary, 12 sections, one class teacher each | «تعيين معلم الصف لمواد شعبة» per section | 0 | 26 | 37 |
| Intermediate, 12 sections, specialists | «اقتراح توزيع الأنصبة» (preview, preview again, apply, confirm) | 0 | 0 | 4 |

## Decisions the owner must confirm
- **Double-period severity (#65):** `DOUBLE_PERIOD_IMPOSSIBLE` is a warning in the standard mode and an error only in the «دروس مزدوجة» mode.
- **One teacher per curriculum line per section (ADR 0032, DECISIONS_PENDING #45):** a line cannot be split between two teachers.
- **Blocked periods apply to lesson numbers in every shift (#57):** a blocked (day, lesson n) blocks lesson n in each shift.
- The other Phase 3 assumptions are in `docs/DECISIONS_PENDING.md` #46–#65.

## Not done
- Nothing from sections B, C and D is left open.

## Known risks
- **The literary curriculum totals from the owner's source do not add up** (DECISIONS_PENDING #41: الرابع الأدبي 27 vs 29, الخامس الأدبي 30 vs 31, السادس الأدبي 30 vs 31). Until they are corrected, readiness reports section capacity mismatches for those stages.
- The validator is deliberately incomplete: conflicts that need combined reasoning over several constraints are left to the Phase 4 solver (ADR 0035).
- The suggester is greedy and deterministic. It can leave lines unassigned (with a reason) when a different global assignment would have fitted.

## Files and folders that look unrelated (not touched)
- `.claude/` (untracked) and `temp_check/` (untracked copy of the repository documents and sources).
