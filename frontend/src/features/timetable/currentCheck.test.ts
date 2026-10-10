import { describe, expect, it } from "vitest";
import { createFormatter } from "../../lib/format";
import { changeItems, findingItems, flaggedSlots, slotKey } from "./currentCheck";
import type { CurrentCheck, CurrentFinding, GridLesson, InputChange } from "./timetableApi";

/** Names carry Unicode isolation marks in sentences; tests compare the visible text. */
const plain = (value: string) => value.replace(/[⁦-⁩]/g, "");
const format = createFormatter({ numeralSystem: "western", calendarDisplay: "gregorian", timeZone: "Asia/Baghdad" });

const names: CurrentCheck["names"] = {
  teachers: [{ id: 1, name: "زينب حسن" }, { id: 2, name: "سعد كاظم" }],
  sections: [{ id: 10, stageName: "الأول المتوسط", label: "أ" }],
  subjects: [{ id: 5, name: "الرياضيات" }],
  stages: [{ id: 3, name: "الأول المتوسط" }],
  resources: [{ id: 8, name: "المختبر" }],
  shifts: [{ id: 4, name: "الدوام الصباحي" }],
};

const finding = (overrides: Partial<CurrentFinding>): CurrentFinding => ({
  code: "TEACHER_UNAVAILABLE", sectionId: null, teacherId: null, currentTeacherId: null, subjectId: null, resourceId: null, day: null, lesson: null, count: null, limit: null, ...overrides,
});
const change = (overrides: Partial<InputChange>): InputChange => ({
  code: "OTHER_CHANGE", teacherId: null, toTeacherId: null, sectionId: null, subjectId: null, stageId: null, shiftId: null, resourceId: null, from: null, to: null, days: null, ...overrides,
});
const check = (findings: CurrentFinding[], changes: InputChange[] = []): CurrentCheck => ({ versionId: 1, stale: true, findings, changes, canReplaceTeachers: false, canRepair: findings.length > 0, names });

describe("current-data check sentences (MF11)", () => {
  it("joins the lessons of one blocked day into one sentence with a count", () => {
    const items = findingItems(check([1, 2, 3, 4].map((lesson) => finding({ sectionId: 10, teacherId: 1, day: 7, lesson }))), format);
    expect(items).toHaveLength(1);
    expect(items[0].sentence).toContain("غير متاح يوم الأحد");
    expect(items[0].sentence).toContain("4 حصص");
    expect(items[0].sentence).toContain("زينب حسن");
    expect(items[0]).toMatchObject({ sectionId: 10, teacherId: 1, day: 7, lesson: 1 });
  });

  it("says which teacher the subject belongs to now and which one the saved timetable still names", () => {
    const items = findingItems(check([
      finding({ code: "TEACHER_REASSIGNED", sectionId: 10, teacherId: 1, currentTeacherId: 2, subjectId: 5, day: 1, lesson: 1 }),
      finding({ code: "TEACHER_REASSIGNED", sectionId: 10, teacherId: 1, currentTeacherId: 2, subjectId: 5, day: 2, lesson: 3 }),
    ]), format);
    expect(items).toHaveLength(1);
    expect(items[0].sentence).toContain("مادة");
    expect(items[0].sentence).toContain("الرياضيات");
    expect(items[0].sentence).toContain("أصبحت للمعلم");
    expect(items[0].sentence).toContain("سعد كاظم");
    expect(items[0].sentence).toContain("والجدول ما زال باسم");
    expect(items[0].sentence).toContain("زينب حسن");
    expect(items[0].sentence).toContain("حصتان");
  });

  it("turns every hard-constraint code into a sentence and falls back for an unknown one, never printing a raw code", () => {
    const codes = ["TEACHER_CONFLICT", "TEACHER_DAY_LIMIT", "TEACHER_WEEK_LIMIT", "ASSIGNMENT_REMOVED", "WRONG_LESSON_COUNT", "OUTSIDE_SECTION_DAY", "SECTION_CONFLICT",
      "SECTION_GAP", "SUBJECT_BLOCKED", "RESOURCE_CAPACITY", "SUBJECT_DAILY_CAP", "DOUBLE_PERIOD_BROKEN", "SOMETHING_NEW"];
    const items = findingItems(check(codes.map((code, index) => finding({ code, sectionId: 10, teacherId: 1, subjectId: 5, resourceId: 8, day: 1, lesson: index + 1, count: 3, limit: 2 }))), format);
    expect(items).toHaveLength(codes.length);
    for (const entry of items) {
      expect(entry.sentence.length).toBeGreaterThan(5);
      expect(entry.sentence).not.toMatch(/[A-Z]{3,}_[A-Z]/);
    }
  });

  it("marks the slots a finding points at, a teacher's day for a day limit, and all of a teacher's lessons for a week limit", () => {
    const lessons: GridLesson[] = [
      { sectionId: 10, lineId: 1, subjectId: 5, teacherId: 1, day: 1, lesson: 1 },
      { sectionId: 10, lineId: 1, subjectId: 5, teacherId: 1, day: 1, lesson: 2 },
      { sectionId: 10, lineId: 1, subjectId: 5, teacherId: 1, day: 2, lesson: 1 },
      { sectionId: 10, lineId: 2, subjectId: 6, teacherId: 2, day: 1, lesson: 3 },
    ];
    expect([...flaggedSlots([finding({ sectionId: 10, day: 1, lesson: 3, teacherId: 2 })], lessons)]).toEqual([slotKey(10, 1, 3)]);
    expect([...flaggedSlots([finding({ code: "TEACHER_DAY_LIMIT", teacherId: 1, day: 1 })], lessons)].sort()).toEqual([slotKey(10, 1, 1), slotKey(10, 1, 2)]);
    expect(flaggedSlots([finding({ code: "TEACHER_WEEK_LIMIT", teacherId: 1 })], lessons).size).toBe(3);
    expect(flaggedSlots([], lessons).size).toBe(0);
  });

  it("describes what changed since the generation in plain sentences grouped by area", () => {
    const items = changeItems(check([], [
      change({ code: "ASSIGNMENT_TEACHER_CHANGED", sectionId: 10, subjectId: 5, teacherId: 1, toTeacherId: 2 }),
      change({ code: "TEACHER_OFF_DAYS_CHANGED", teacherId: 1, days: [7] }),
      change({ code: "TEACHER_WEEK_LIMIT_CHANGED", teacherId: 1, from: null, to: 18 }),
      change({ code: "LESSONS_PER_WEEK_CHANGED", subjectId: 5, stageId: 3, from: 4, to: 6 }),
      change({ code: "SHIFT_TIMING_CHANGED", shiftId: 4 }),
      change({ code: "PRIORITIES_CHANGED" }),
    ]), format);
    expect(items.map((entry) => entry.area)).toEqual(["assignments", "availability", "loads", "curriculum", "timing", "other"]);
    expect(plain(items[0].sentence)).toContain("انتقلت من زينب حسن إلى سعد كاظم");
    expect(items[1].sentence).toContain("الأحد");
    expect(items[2].sentence).toContain("بلا حد");
    expect(items[3].sentence).toContain("6 حصص");
    expect(items[3].sentence).toContain("4 حصص");
  });
});
