import { describe, expect, it } from "vitest";
import { breakClock, breakIssues, freePosition, validBreaks } from "./BreaksEditor";

describe("breaks editor (R2: any gap, any number, 1–60 minutes)", () => {
  it("proposes the first free gap, preferring after the third lesson", () => {
    expect(freePosition(7, [])).toBe(3);
    expect(freePosition(7, [{ afterLesson: 3, minutes: 15 }])).toBe(4);
    expect(freePosition(3, [{ afterLesson: 2, minutes: 15 }])).toBe(1);
    expect(freePosition(2, [{ afterLesson: 1, minutes: 15 }])).toBeNull();
    expect(freePosition(1, [])).toBeNull();
  });

  it("allows a break in every gap and reports a duplicate gap or a break after the last lesson", () => {
    const everyGap = [1, 2, 3, 4, 5].map((afterLesson) => ({ afterLesson, minutes: afterLesson }));
    expect(breakIssues(6, everyGap)).toEqual([null, null, null, null, null]);
    expect(validBreaks(6, everyGap)).toHaveLength(5);
    expect(breakIssues(6, [{ afterLesson: 2, minutes: 5 }, { afterLesson: 2, minutes: 10 }])).toEqual([null, "duplicate"]);
    expect(breakIssues(3, [{ afterLesson: 1, minutes: 5 }, { afterLesson: 3, minutes: 10 }])).toEqual([null, "afterLast"]);
    expect(validBreaks(3, [{ afterLesson: 1, minutes: 5 }, { afterLesson: 3, minutes: 10 }])).toEqual([{ afterLesson: 1, minutes: 5 }]);
  });

  it("computes each break's clock time from the start, lessons, earlier breaks and gaps", () => {
    const breaks = [{ afterLesson: 1, minutes: 1 }, { afterLesson: 3, minutes: 60 }];
    expect(breakClock("08:00", 45, 0, breaks, breaks[0])).toEqual({ start: "08:45", end: "08:46" });
    expect(breakClock("08:00", 45, 0, breaks, breaks[1])).toEqual({ start: "10:16", end: "11:16" });
    expect(breakClock("08:00", 45, 5, breaks, breaks[1])).toEqual({ start: "10:21", end: "11:21" }); // one gap (lessons 2→3)
    expect(breakClock("", 45, 0, breaks, breaks[0])).toBeNull();
  });
});
