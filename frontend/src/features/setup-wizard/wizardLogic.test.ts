import { describe, expect, it } from "vitest";
import type { Shift } from "../timetable-structure/scheduleApi";
import { planFromPreset, planFromShift, toShiftInput, weeklyLessons } from "./timingPlan";
import { proposeYear, proposedStartYear, yearChoices } from "./yearProposal";

const terms = ["الفصل الأول", "الفصل الثاني"];
const sundayToThursday = [7, 1, 2, 3, 4];

describe("wizard year proposal", () => {
  it("starts the proposed year in September of this year from July, otherwise of last year", () => {
    expect(proposedStartYear("2026-10-04")).toBe(2026);
    expect(proposedStartYear("2027-03-15")).toBe(2026);
    expect(proposedStartYear("2027-07-01")).toBe(2027);
    expect(yearChoices("2026-10-04")).toEqual([2025, 2026, 2027]);
  });

  it("proposes the year dates and two terms that the owner can edit", () => {
    expect(proposeYear(2026, terms)).toEqual({
      label: "2026-2027",
      startDate: "2026-09-01",
      endDate: "2027-06-30",
      terms: [
        { name: "الفصل الأول", startDate: "2026-09-01", endDate: "2027-01-15" },
        { name: "الفصل الثاني", startDate: "2027-02-01", endDate: "2027-06-30" },
      ],
    });
  });
});

describe("wizard timing plan", () => {
  const preset = { key: "after-3", name: "بعد الثالثة", firstStart: "08:00", lessonMinutes: 45, lessonCount: 7, breaks: [{ afterLesson: 3, minutes: 15 }] };

  it("counts weekly lessons with per-day exceptions and sends every working day", () => {
    const plan = { ...planFromPreset(preset, "08:00"), dayLessons: { 4: 5 } };
    expect(weeklyLessons(plan, sundayToThursday)).toBe(33);
    const input = toShiftInput("morning", { ...plan, lessonCount: 3 }, sundayToThursday);
    expect(input.breaks).toEqual([]); // a break after the last lesson is dropped
    expect(input.dayLessons).toEqual(sundayToThursday.map((day) => ({ day, lessons: 3 })));
  });

  it("reads a saved shift back into the same plan (resume)", () => {
    const shift: Shift = {
      id: 1, academicYearId: 1, name: "الدوام الصباحي", displayOrder: 1, kind: "morning", lessonCount: 3, weeklyLessons: 14, version: 3,
      periods: [
        { position: 1, kind: "lesson", startTime: "08:00", endTime: "08:45", startBell: true, endBell: true },
        { position: 2, kind: "lesson", startTime: "08:45", endTime: "09:30", startBell: true, endBell: true },
        { position: 3, kind: "break", startTime: "09:30", endTime: "09:45", startBell: true, endBell: true },
        { position: 4, kind: "lesson", startTime: "09:45", endTime: "10:30", startBell: true, endBell: true },
      ],
      dayLessons: [{ day: 7, lessons: 3 }, { day: 4, lessons: 2 }],
    };
    expect(planFromShift(shift)).toEqual({ presetKey: "", firstStartTime: "08:00", lessonCount: 3, lessonMinutes: 45, breaks: [{ afterLesson: 2, minutes: 15 }], dayLessons: { 4: 2 } });
    expect(planFromShift({ ...shift, periods: [] })).toBeNull();
  });
});
