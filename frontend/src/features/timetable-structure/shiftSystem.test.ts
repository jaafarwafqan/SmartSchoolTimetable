import { describe, expect, it } from "vitest";
import type { PeriodPreset } from "../curriculum/curriculumApi";
import { applyPreset, blankPlan, defaultMorningDays, initialValue, noPreset, presetsFor, toCommand, type ShiftSystemValue } from "./shiftSystem";
import type { Shift } from "./scheduleApi";

const presets: PeriodPreset[] = [
  { key: "single-break-after-3", name: "صباحي", firstStart: "08:00", lessonMinutes: 45, lessonCount: 7, breaks: [{ afterLesson: 3, minutes: 15 }], session: "morning" },
  { key: "evening-single-break", name: "مسائي", firstStart: "13:00", lessonMinutes: 40, lessonCount: 6, breaks: [{ afterLesson: 3, minutes: 10 }], session: "evening" },
];
const days = [7, 1, 2, 3, 4];

describe("timing templates (MF1)", () => {
  it("lists only the templates of the session; the evening template never appears for the morning", () => {
    expect(presetsFor(presets, "morning").map((preset) => preset.key)).toEqual(["single-break-after-3"]);
    expect(presetsFor(presets, "evening").map((preset) => preset.key)).toEqual(["evening-single-break"]);
  });

  it("starts from «بدون قالب» with no breaks, and choosing it again clears the breaks but keeps the per-day counts", () => {
    const blank = blankPlan("morning");
    expect(blank.breaks).toEqual([]);
    expect(blank.presetKey).toBe(noPreset);
    const chosen = applyPreset({ ...blank, dayLessons: { 4: 5 } }, presets, "single-break-after-3", "morning");
    expect(chosen.lessonCount).toBe(7);
    expect(chosen.breaks).toEqual([{ afterLesson: 3, minutes: 15 }]);
    expect(chosen.dayLessons).toEqual({ 4: 5 });
    const cleared = applyPreset(chosen, presets, noPreset, "morning");
    expect(cleared.breaks).toEqual([]);
    expect(cleared.lessonCount).toBe(7);
  });
});

describe("shift system command (MF7)", () => {
  const value: ShiftSystemValue = {
    system: "dual",
    main: { ...blankPlan("morning", 7), breaks: [{ afterLesson: 3, minutes: 15 }], dayLessons: { 4: 6 } },
    evening: { ...blankPlan("evening", 4), breaks: [{ afterLesson: 2, minutes: 10 }, { afterLesson: 9, minutes: 5 }] },
    morningDays: defaultMorningDays(days),
  };

  it("sends one shift; «مزدوج» adds the evening timing with the morning lesson count and a full mapping", () => {
    const command = toCommand(value, days);
    expect(command.system).toBe("dual");
    expect(command.main.kind).toBe("morning");
    expect(command.main.dayLessons.find((day) => day.day === 4)?.lessons).toBe(6);
    expect(command.evening?.lessonCount).toBe(7);
    expect(command.evening?.firstStartTime).toBe("13:00");
    expect(command.evening?.breaks).toEqual([{ afterLesson: 2, minutes: 10 }]); // a break after the last lesson is dropped
    expect(command.sessionDays).toHaveLength(10);
    expect(command.sessionDays?.filter((day) => day.term === 1 && day.session === "morning").map((day) => day.day)).toEqual([7, 1, 2]);
    expect(command.sessionDays?.filter((day) => day.term === 2 && day.session === "morning").map((day) => day.day)).toEqual([3, 4]);
  });

  it("sends only the main timing for morning and evening schools", () => {
    expect(toCommand({ ...value, system: "morning" }, days)).toEqual({ system: "morning", main: expect.objectContaining({ kind: "morning" }) });
    expect(toCommand({ ...value, system: "evening" }, days).main.kind).toBe("evening");
  });

  it("reads the stored shift and session plan back", () => {
    const shift = {
      id: 1, academicYearId: 1, name: "الدوام المزدوج", displayOrder: 1, kind: "morning", lessonCount: 2, weeklyLessons: 10, version: 1,
      periods: [
        { position: 1, kind: "lesson", startTime: "08:00", endTime: "08:45", startBell: true, endBell: true },
        { position: 2, kind: "lesson", startTime: "08:45", endTime: "09:30", startBell: true, endBell: true },
      ],
      dayLessons: days.map((day) => ({ day, lessons: 2 })),
    } as unknown as Shift;
    const start = initialValue("dual", shift, {
      system: "twoSessions", shiftId: 1, available: true, lessonCount: 2, workingDays: days, version: 3,
      timings: [{ session: "evening", periods: [{ position: 1, kind: "lesson", startTime: "13:30", endTime: "14:10" }, { position: 2, kind: "lesson", startTime: "14:10", endTime: "14:50" }] }],
      days: days.flatMap((day) => [{ term: 1 as const, day, session: day === 7 ? "morning" as const : "evening" as const }, { term: 2 as const, day, session: day === 7 ? "evening" as const : "morning" as const }]),
    }, days);
    expect(start.main.lessonCount).toBe(2);
    expect(start.main.firstStartTime).toBe("08:00");
    expect(start.evening.lessonMinutes).toBe(40);
    expect(start.evening.firstStartTime).toBe("13:30");
    expect(start.morningDays).toEqual({ 1: [7], 2: [1, 2, 3, 4] });
  });
});
