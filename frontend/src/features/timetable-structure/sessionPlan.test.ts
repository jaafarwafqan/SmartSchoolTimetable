import { describe, expect, it } from "vitest";
import { buildPeriods, mappingOf, morningDays, reversedMorning, timingForm, type SessionDay } from "./sessionPlan";

const week = [7, 1, 2, 3, 4];

describe("R3 session timing", () => {
  it("builds evening rows with the same lesson count, breaks and a gap", () => {
    const rows = buildPeriods({ firstStart: "13:00", lessonMinutes: 40, breaks: [{ afterLesson: 3, minutes: 10 }], gapMinutes: 5 }, 6);
    expect(rows.filter((row) => row.kind === "lesson")).toHaveLength(6);
    expect(rows[0]).toEqual({ kind: "lesson", startTime: "13:00", endTime: "13:40" });
    expect(rows[1]).toEqual({ kind: "lesson", startTime: "13:45", endTime: "14:25" });
    expect(rows[3]).toEqual({ kind: "break", startTime: "15:10", endTime: "15:20" });
    expect(rows.at(-1)).toEqual({ kind: "lesson", startTime: "16:50", endTime: "17:30" });
  });

  it("ignores a break after the last lesson and refuses an invalid start", () => {
    expect(buildPeriods({ firstStart: "13:00", lessonMinutes: 40, breaks: [{ afterLesson: 2, minutes: 10 }], gapMinutes: 0 }, 2)).toHaveLength(2);
    expect(buildPeriods({ firstStart: "", lessonMinutes: 40, breaks: [], gapMinutes: 0 }, 6)).toEqual([]);
  });

  it("reads saved rows back into the same form", () => {
    const form = { firstStart: "12:30", lessonMinutes: 35, breaks: [{ afterLesson: 2, minutes: 15 }, { afterLesson: 4, minutes: 5 }], gapMinutes: 0 };
    const rows = buildPeriods(form, 6).map((row, index) => ({ ...row, position: index + 1 }));
    expect(timingForm(rows, "13:00", 40)).toEqual(form);
    expect(timingForm([], "13:00", 40)).toEqual({ firstStart: "13:00", lessonMinutes: 40, breaks: [], gapMinutes: 0 });
  });
});

describe("R3 day mapping", () => {
  it("keeps the owner's example and reverses it in one step", () => {
    const first = [7, 1];
    const second = reversedMorning(first, week);
    expect(second).toEqual([2, 3, 4]);
    const days = mappingOf({ 1: first, 2: second }, week);
    expect(days).toHaveLength(10);
    expect(days.find((item) => item.term === 1 && item.day === 2)?.session).toBe("evening");
    expect(days.find((item) => item.term === 2 && item.day === 7)?.session).toBe("evening");
    expect(morningDays(days, 1, week)).toEqual([7, 1]);
    expect(morningDays(days, 2, week)).toEqual([2, 3, 4]);
  });

  it("starts with every day in the morning when nothing is mapped", () => {
    const none: SessionDay[] = [];
    expect(morningDays(none, 1, week)).toEqual(week);
  });
});
