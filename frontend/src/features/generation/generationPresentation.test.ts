import { describe, expect, it } from "vitest";
import { messages } from "../../i18n/messages";
import { createFormatter } from "../../lib/format";
import { diagnosticCodes, isActive, type GenerationRun, type SolverFinding } from "./generationApi";
import { diagnosticMessage, elapsedSeconds, phaseIndex, statusNote } from "./generationPresentation";

const format = createFormatter();
const text = messages.school.generation;

function run(overrides: Partial<GenerationRun>): GenerationRun {
  return {
    id: 1, academicYearId: 1, status: "completed", mode: "standard", timeLimitSeconds: 60, seed: 7, workers: 2, deterministic: false,
    solverVersion: "OR-Tools 9.15", inputHash: "abc", profileVersion: 1, queuedAt: "2026-10-09T08:00:00Z", startedAt: "2026-10-09T08:00:00Z",
    finishedAt: null, elapsedSeconds: null, objective: null, bound: null, optimal: false, improvements: 0, firstSolutionSeconds: null,
    lessonsPlaced: 0, score: null, diagnostics: null, errorCode: null, timetableVersionId: null, live: null, ...overrides,
  };
}

describe("generation progress", () => {
  it("shows only real counters: the stepper follows the live phase and the timer the server start time", () => {
    const live = run({ status: "generating", live: { phase: "generating", elapsedSeconds: 3, improvements: 2, bestObjective: 40, bestBound: 10, firstSolutionSeconds: 1.2 } });
    expect(isActive(live)).toBe(true);
    expect(phaseIndex(live)).toBe(2);
    expect(elapsedSeconds(live, Date.parse("2026-10-09T08:00:05Z"))).toBe(5);
    expect(phaseIndex(run({ status: "completed" }))).toBe(4);
    expect(isActive(run({ status: "timedOut" }))).toBe(false);
  });

  it("explains each final status in Arabic, never a false 'impossible' on a timeout", () => {
    expect(statusNote(run({ status: "completed", optimal: true }))).toBe(text.statusNotes.completedOptimal);
    expect(statusNote(run({ status: "timedOut" }))).toBe(text.statusNotes.timedOut);
    expect(statusNote(run({ status: "cancelled", timetableVersionId: null }))).toBe(text.statusNotes.cancelledEmpty);
  });

  it("renders every diagnostic code as Arabic with Arabic numerals", () => {
    for (const code of diagnosticCodes) {
      const finding: SolverFinding = { code, entity: { kind: "teacher", id: 1, name: "أحمد" }, related: [], required: 5, available: 3, fixes: [], relaxationHelps: true };
      const message = diagnosticMessage(finding, format);
      expect(message, code).not.toBe(text.unknownDiagnostic);
      expect(message, code).not.toMatch(/[A-Za-z]/);
    }
    const teacher: SolverFinding = { code: "CORE_TEACHER_AVAILABILITY", entity: { kind: "teacher", id: 1, name: "أحمد" }, related: [], required: 5, available: 3, fixes: [], relaxationHelps: null };
    expect(diagnosticMessage(teacher, format)).toContain("٥ حصص");
    expect(diagnosticMessage({ ...teacher, code: "SOMETHING_NEW" }, format)).toBe(text.unknownDiagnostic);
  });
});
