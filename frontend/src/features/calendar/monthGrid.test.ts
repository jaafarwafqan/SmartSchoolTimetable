import { describe, expect, it } from "vitest";
import { isoWeekday, monthEnd, monthGrid, monthStart } from "./monthGrid";

describe("month grid", () => {
  it("computes ISO weekdays and month bounds without time-zone shifts", () => {
    expect(isoWeekday("2026-10-04")).toBe(7); // Sunday
    expect(isoWeekday("2026-10-05")).toBe(1);
    expect(monthStart("2026-10-17")).toBe("2026-10-01");
    expect(monthStart("2026-12-17", 1)).toBe("2027-01-01");
    expect(monthStart("2027-01-03", -1)).toBe("2026-12-01");
    expect(monthEnd("2028-02-01")).toBe("2028-02-29");
  });

  it("lays the month out in weeks starting on the school's week start day", () => {
    const sundayFirst = monthGrid("2026-10-01", 7); // 1 October 2026 is a Thursday
    expect(sundayFirst[0]).toEqual([null, null, null, null, "2026-10-01", "2026-10-02", "2026-10-03"]);
    expect(sundayFirst.flat().filter(Boolean)).toHaveLength(31);
    expect(sundayFirst.every((week) => week.length === 7)).toBe(true);
    expect(monthGrid("2026-10-01", 4)[0][0]).toBe("2026-10-01"); // Thursday-first week
  });
});
