import { describe, expect, it } from "vitest";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { createFormatter } from "../../lib/format";
import { diagnosticCodes, isActive, type GenerationRun, type SolverFinding } from "./generationApi";
import { diagnosticMessage, elapsedSeconds, improvementNotes, phaseIndex, resultHeadline, statusNote } from "./generationPresentation";

const format = createFormatter();
const text = messages.school.generation;

function run(overrides: Partial<GenerationRun>): GenerationRun {
  return {
    id: 1, academicYearId: 1, status: "completed", mode: "standard", timeLimitSeconds: 60, seed: 7, workers: 2, deterministic: false,
    solverVersion: "OR-Tools 9.15", inputHash: "abc", profileVersion: 1, queuedAt: "2026-10-09T08:00:00Z", startedAt: "2026-10-09T08:00:00Z",
    finishedAt: null, elapsedSeconds: null, objective: null, bound: null, optimal: false, improvements: 0, firstSolutionSeconds: null,
    lessonsPlaced: 0, score: null, diagnostics: null, errorCode: null, timetableVersionId: null, live: null, lockedFromVersionId: null, isRepair: false, lockedLessons: 0, locksDropped: 0, ...overrides,
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

describe("plain result (MF3)", () => {
  const rules = [
    { key: "avoidTeacherGaps", enabled: true, weight: 10, penalty: 4, weighted: 40 },
    { key: "avoidSameSubjectRepeated", enabled: true, weight: 30, penalty: 2, weighted: 60 },
    { key: "heavySubjectsEarly", enabled: false, weight: 5, penalty: 9, weighted: 0 },
    { key: "spreadSubjectsAcrossDays", enabled: true, weight: 20, penalty: 0, weighted: 0 },
  ];

  it("says the timetable is valid and ready in plain words, without solver jargon", () => {
    const done = resultHeadline(run({ status: "completed", timetableVersionId: 3, optimal: false }));
    expect(done).toEqual({ ok: true, title: text.plain.ready, sentence: text.plain.readyGood });
    expect(resultHeadline(run({ status: "completed", timetableVersionId: 3, optimal: true })).sentence).toBe(text.plain.readyBest);
    expect(resultHeadline(run({ status: "cancelled", timetableVersionId: 3 })).sentence).toBe(text.plain.readyStopped);
    expect(done.sentence).not.toContain("إثبات");
  });

  it("names the failure reason when no timetable was saved", () => {
    expect(resultHeadline(run({ status: "infeasible", timetableVersionId: null }))).toEqual({ ok: false, title: text.plain.noTimetable.infeasible, sentence: text.plain.noTimetableHint.infeasible });
    expect(resultHeadline(run({ status: "timedOut", timetableVersionId: null })).title).toBe(text.plain.noTimetable.timedOut);
  });

  it("turns the remaining penalties into sentences, heaviest first, skipping disabled and clean rules", () => {
    const notes = improvementNotes(run({ score: { total: 100, rules } }), format);
    expect(notes.map((note) => note.sentence)).toEqual([
      text.plain.notes.avoidSameSubjectRepeated("مرتين"),
      text.plain.notes.avoidTeacherGaps("٤ فراغات"),
    ]);
    expect(improvementNotes(run({ score: null }), format)).toEqual([]);
  });

  it("writes plain Arabic notes for the teacher rules: adjacent lessons, uneven days and the last periods with names and counts", () => {
    const score = {
      total: 300,
      rules: [
        { key: "spreadTeacherLessons", enabled: true, weight: 30, penalty: 3, weighted: 90 },
        { key: "balanceTeacherDays", enabled: true, weight: 40, penalty: 2, weighted: 80 },
        { key: "fairLastPeriods", enabled: true, weight: 20, penalty: 2, weighted: 40 },
      ],
      lastPeriods: { mostTeacher: "رغد", mostCount: 4, leastTeacher: "سلمى", leastCount: 1 },
    };
    const notes = improvementNotes(run({ score }), format).map((note) => note.sentence);
    expect(notes).toEqual([
      text.plain.notes.spreadTeacherLessons("٣ مرات"),
      text.plain.notes.balanceTeacherDays("حصتان"),
      `أكثر معلم في الحصص الأخيرة: ${isolate("رغد")} (٤)، وأقلهم: ${isolate("سلمى")} (١).`,
    ]);
    // Without last-period details there is nothing to name, so no sentence is invented.
    expect(improvementNotes(run({ score: { ...score, lastPeriods: null } }), format)).toHaveLength(2);
    // A fair timetable has no note about it.
    expect(improvementNotes(run({ score: { ...score, rules: [{ ...score.rules[2], penalty: 0, weighted: 0 }] } }), format)).toEqual([]);
  });

  it("counts same-day repeats in plain Arabic, splits unavoidable from improvable and names the teacher, the subject and the reason", () => {
    const score = {
      total: 1600,
      rules: [{ key: "spreadSubjectsAcrossDays", enabled: true, weight: 40, penalty: 40, weighted: 1600 }],
      repeats: {
        total: 37, unavoidable: 30, improvable: 7,
        forced: [
          { teacher: "رغد", subject: "الرياضيات", reason: "moreLessonsThanDays", weekly: 6, days: 5, count: 3, sections: 3 },
          { teacher: "سلمى", subject: "العلوم", reason: "teacherAbsent", weekly: 6, days: 4, count: 2, sections: 1 },
        ],
      },
    };
    const notes = improvementNotes(run({ score }), format).map((note) => note.sentence);
    expect(notes).toEqual([
      "تكررت المادة في اليوم نفسه للشعبة ٣٧ مرة: ٣٠ تكراراً لا مفرّ منها، و٧ تكرارات يمكن تحسينها.",
      `لا مفرّ منها: ${isolate("الرياضيات")} مع ${isolate("رغد")}: ٦ حصص أسبوعياً وأيام الدوام ٥ أيام (٣ شعب)؛ `
        + `${isolate("العلوم")} مع ${isolate("سلمى")}: ٦ حصص أسبوعياً والمعلم حاضر ٤ أيام فقط (شعبة واحدة).`,
    ]);
    const forcedOnly = { ...score, repeats: { ...score.repeats, total: 30, improvable: 0 } };
    expect(improvementNotes(run({ score: forcedOnly }), format)[0].sentence).toBe("تكررت المادة في اليوم نفسه للشعبة ٣٠ مرة، وكلها لا مفرّ منها.");
    // An older run without the details keeps the old sentence.
    expect(improvementNotes(run({ score: { ...score, repeats: null } }), format)[0].sentence).toBe(text.plain.notes.spreadSubjectsAcrossDays("٤٠ مرة"));
  });

  it("explains that a full week gives every section a last lesson every day", () => {
    const score = {
      total: 40,
      rules: [{ key: "fairLastPeriods", enabled: true, weight: 20, penalty: 2, weighted: 40 }],
      lastPeriods: { mostTeacher: "رغد", mostCount: 6, leastTeacher: "سلمى", leastCount: 1, fullWeek: true, lastLessons: 55 },
    };
    const sentence = improvementNotes(run({ score }), format)[0].sentence;
    expect(sentence).toContain("فلكل شعبة حصة أخيرة في كل يوم: ٥٥ حصة في الأسبوع");
    expect(sentence).toContain(`أكثر معلم في الحصص الأخيرة: ${isolate("رغد")} (٦)`);
  });

  it("lists every teacher over the consecutive-lessons limit with the day and the count, and names the likely cause after a timeout", () => {
    const score = {
      total: 80,
      rules: [{ key: "maxConsecutiveLessons", enabled: true, weight: 40, penalty: 2, weighted: 80 }],
      consecutiveRuns: [{ teacher: "رغد", day: 7, count: 4, limit: 3 }, { teacher: "رغد", day: 2, count: 5, limit: 3 }, { teacher: "سلمى", day: 1, count: 4, limit: 3 }],
    };
    expect(improvementNotes(run({ score }), format).map((note) => note.sentence)).toEqual([
      `معلمون تجاوزوا حد الحصص المتتالية (أطول تتابع في اليوم): ${isolate("رغد")} (الأحد: ٤، الثلاثاء: ٥)؛ ${isolate("سلمى")} (الاثنين: ٤).`,
    ]);
    const cause: SolverFinding = { code: "CONSECUTIVE_LIKELY_CAUSE", entity: { kind: "school", id: 0, name: "" }, related: [{ kind: "teacher", id: 1, name: "رغد" }], required: null, available: null, fixes: ["relaxConsecutive"], relaxationHelps: true };
    const message = diagnosticMessage(cause, format);
    expect(message).toContain("السبب على الأرجح");
    expect(message).toContain(isolate("رغد"));
  });

  it("explains the consecutive and balance diagnostics with the teacher's name", () => {
    const finding = (code: string): SolverFinding => ({ code, entity: { kind: "teacher", id: 1, name: "رغد" }, related: [], required: 20, available: 15, fixes: [], relaxationHelps: true });
    expect(diagnosticMessage(finding("CORE_TEACHER_CONSECUTIVE"), format)).toContain("أقصى حصص متتالية للمعلم");
    expect(diagnosticMessage(finding("CORE_TEACHER_CONSECUTIVE"), format)).toContain("المطلوب ٢٠ حصة، والمتاح ١٥ حصة");
    expect(diagnosticMessage(finding("CORE_TEACHER_BALANCE"), format)).toContain(isolate("رغد"));
  });
});
