import { describe, expect, it } from "vitest";
import { changedCount, moveOrSwap, push, redo, startHistory, undo } from "./editing";
import type { GridLesson } from "./timetableApi";

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
