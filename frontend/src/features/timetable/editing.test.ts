import { describe, expect, it } from "vitest";
import { changedCount, conflictKeys, moveOrSwap, push, redo, startHistory, undo } from "./editing";
import type { GridLesson, Violation } from "./timetableApi";

const lesson = (lineId: number, day: number, number: number, sectionId = 1): GridLesson => ({ sectionId, lineId, subjectId: lineId, teacherId: lineId, day, lesson: number });

describe("manual timetable edits", () => {
  const base = [lesson(10, 7, 1), lesson(20, 7, 2), lesson(30, 1, 1)];

  it("moves a lesson into an empty slot of the same section", () => {
    const moved = moveOrSwap(base, { sectionId: 1, day: 7, lesson: 2 }, { sectionId: 1, day: 1, lesson: 2 });
    expect(moved.find((item) => item.lineId === 20)).toMatchObject({ day: 1, lesson: 2 });
    expect(changedCount(base, moved)).toBe(1);
  });

  it("swaps two lessons and never moves across sections", () => {
    const swapped = moveOrSwap(base, { sectionId: 1, day: 7, lesson: 1 }, { sectionId: 1, day: 7, lesson: 2 });
    expect(swapped.find((item) => item.lineId === 10)).toMatchObject({ lesson: 2 });
    expect(swapped.find((item) => item.lineId === 20)).toMatchObject({ lesson: 1 });
    expect(changedCount(base, swapped)).toBe(2);
    expect(moveOrSwap(base, { sectionId: 1, day: 7, lesson: 1 }, { sectionId: 2, day: 7, lesson: 1 })).toEqual(base);
  });

  it("undoes and redoes every step in order", () => {
    let history = startHistory(base);
    history = push(history, moveOrSwap(history.present, { sectionId: 1, day: 7, lesson: 1 }, { sectionId: 1, day: 7, lesson: 2 }));
    history = push(history, moveOrSwap(history.present, { sectionId: 1, day: 1, lesson: 1 }, { sectionId: 1, day: 1, lesson: 2 }));
    expect(changedCount(base, history.present)).toBe(3);
    history = undo(history);
    expect(changedCount(base, history.present)).toBe(2);
    history = undo(history);
    expect(history.present).toEqual(base);
    expect(undo(history)).toBe(history);
    history = redo(redo(history));
    expect(changedCount(base, history.present)).toBe(3);
    expect(redo(history)).toBe(history);
  });
});

describe("editing from every view (owner, 1.0.0 testing)", () => {
  const lesson = (sectionId: number, teacherId: number, day: number, number: number, lineId = sectionId * 10): GridLesson =>
    ({ sectionId, lineId, subjectId: 1, teacherId, day, lesson: number });
  const violation = (overrides: Partial<Violation>): Violation => ({ code: "TEACHER_CONFLICT", rule: "H4", sectionId: null, teacherId: null, subjectId: null, resourceId: null, day: null, lesson: null, count: null, limit: null, ...overrides });

  it("marks a teacher conflict on that teacher's lessons in every section, at that day and lesson only", () => {
    const lessons = [lesson(1, 7, 7, 2), lesson(2, 7, 7, 2, 21), lesson(2, 8, 7, 3), lesson(3, 7, 1, 2)];
    expect(conflictKeys([violation({ teacherId: 7, day: 7, lesson: 2 })], lessons)).toEqual(new Set(["1:7:2", "2:7:2"]));
    // A daily limit (no lesson number) marks all his lessons of that day.
    expect(conflictKeys([violation({ code: "TEACHER_DAY_LIMIT", teacherId: 7, day: 7 })], lessons)).toEqual(new Set(["1:7:2", "2:7:2"]));
    // A subject limit in one section does not mark the same subject elsewhere.
    expect(conflictKeys([violation({ code: "SUBJECT_DAILY_CAP", subjectId: 1, sectionId: 2, day: 7 })], lessons)).toEqual(new Set(["2:7:2", "2:7:3"]));
    expect(conflictKeys([violation({ day: null, teacherId: 7 })], lessons).size).toBe(0);
  });

  it("a move chosen in the teacher view happens in the lesson's own section and swaps with what is there", () => {
    const mine = lesson(1, 7, 7, 1);
    const other = lesson(1, 8, 1, 3, 11);
    const elsewhere = lesson(2, 9, 1, 3, 21);
    const moved = moveOrSwap([mine, other, elsewhere], { sectionId: 1, day: 7, lesson: 1 }, { sectionId: 1, day: 1, lesson: 3 });
    expect(moved).toContainEqual({ ...mine, day: 1, lesson: 3 });
    expect(moved).toContainEqual({ ...other, day: 7, lesson: 1 });
    expect(moved).toContainEqual(elsewhere); // another section's lesson at that slot is untouched
    // Never across sections.
    expect(moveOrSwap([mine, elsewhere], { sectionId: 1, day: 7, lesson: 1 }, { sectionId: 2, day: 1, lesson: 3 })).toEqual([mine, elsewhere]);
  });
});
