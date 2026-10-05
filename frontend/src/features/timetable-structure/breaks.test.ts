import { describe, expect, it } from "vitest";
import type { Shift } from "./scheduleApi";
import { freePosition, maxBreaks, validBreaks } from "./BreaksEditor";
import { planFromShift, toShiftInput } from "../setup-wizard/timingPlan";

describe("break model (ADR 0026)", () => {
  it("places a new break after the first free lesson, never twice after the same lesson", () => {
    expect(freePosition(7, [])).toBe(3);
    expect(freePosition(7, [{ afterLesson: 3, minutes: 15 }])).toBe(4);
    expect(freePosition(3, [{ afterLesson: 2, minutes: 15 }])).toBe(1);
    expect(freePosition(2, [{ afterLesson: 1, minutes: 15 }])).toBeNull();
    expect(freePosition(1, [])).toBeNull();
  });

  it("drops breaks after the last lesson and keeps at most three", () => {
    const breaks = [{ afterLesson: 1, minutes: 5 }, { afterLesson: 2, minutes: 10 }, { afterLesson: 3, minutes: 20 }, { afterLesson: 4, minutes: 5 }];
    expect(validBreaks(6, breaks)).toHaveLength(maxBreaks);
    expect(validBreaks(3, breaks)).toEqual([{ afterLesson: 1, minutes: 5 }, { afterLesson: 2, minutes: 10 }]);
  });

  it("reads each break's own duration and the gap back from saved periods; each shift keeps its own plan", () => {
    const lesson = (position: number, start: string, end: string) => ({ position, kind: "lesson" as const, startTime: start, endTime: end, startBell: true, endBell: true });
    const shift = (kind: "morning" | "evening", periods: Shift["periods"]): Shift => ({
      id: 1, academicYearId: 1, name: kind, displayOrder: 1, kind, lessonCount: 3, weeklyLessons: 15, version: 1, periods, dayLessons: [],
    });
    const morning = planFromShift(shift("morning", [
      lesson(1, "08:00", "08:40"), lesson(2, "08:45", "09:25"),
      { position: 3, kind: "break", startTime: "09:25", endTime: "09:45", startBell: false, endBell: false },
      lesson(4, "09:45", "10:25"),
    ]));
    const evening = planFromShift(shift("evening", [lesson(1, "13:00", "13:40"), { position: 2, kind: "break", startTime: "13:40", endTime: "13:50", startBell: false, endBell: false }, lesson(3, "13:50", "14:30")]));
    expect(morning).toMatchObject({ gapMinutes: 5, breaks: [{ afterLesson: 2, minutes: 20 }], lessonMinutes: 40 });
    expect(evening).toMatchObject({ gapMinutes: 0, breaks: [{ afterLesson: 1, minutes: 10 }] });
    expect(toShiftInput("morning", morning!, [7, 1])).toMatchObject({ gapMinutes: 5, breaks: [{ afterLesson: 2, minutes: 20 }] });
  });
});
