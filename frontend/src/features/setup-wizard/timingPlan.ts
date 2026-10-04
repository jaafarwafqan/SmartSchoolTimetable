import type { BreakSlot, PeriodPreset } from "../curriculum/curriculumApi";
import type { Period, Shift } from "../timetable-structure/scheduleApi";
import type { WizardShiftInput } from "./wizardApi";

export type ShiftPlan = {
  presetKey: string;
  firstStartTime: string;
  lessonCount: number;
  lessonMinutes: number;
  breaks: BreakSlot[];
  /** Lessons per working day; a missing day teaches the full count. */
  dayLessons: Record<number, number>;
};

export const maxLessonsPerDay = 12;
export const lessonMinuteChoices = [30, 35, 40, 45, 50, 55, 60] as const;

const toMinutes = (time: string) => {
  const [hours, minutes] = time.split(":").map(Number);
  return hours * 60 + minutes;
};

/** The plan of a preset (or plain defaults), starting at the given time. */
export function planFromPreset(preset: PeriodPreset | undefined, firstStartTime: string): ShiftPlan {
  return {
    presetKey: preset?.key ?? "",
    firstStartTime,
    lessonCount: preset?.lessonCount ?? 6,
    lessonMinutes: preset?.lessonMinutes ?? 45,
    breaks: preset?.breaks ?? [],
    dayLessons: {},
  };
}

/** Reads a saved shift back into a plan so a resumed wizard shows what is stored (null when it has no periods). */
export function planFromShift(shift: Shift): ShiftPlan | null {
  const periods: Period[] = [...shift.periods].sort((a, b) => a.position - b.position);
  const lessons = periods.filter((period) => period.kind === "lesson");
  if (lessons.length === 0) return null;
  const breaks: BreakSlot[] = [];
  let taught = 0;
  for (const period of periods) {
    if (period.kind === "lesson") taught++;
    else if (taught > 0) breaks.push({ afterLesson: taught, minutes: toMinutes(period.endTime) - toMinutes(period.startTime) });
  }
  return {
    presetKey: "",
    firstStartTime: periods[0].startTime,
    lessonCount: lessons.length,
    lessonMinutes: toMinutes(lessons[0].endTime) - toMinutes(lessons[0].startTime),
    breaks,
    dayLessons: Object.fromEntries(shift.dayLessons.filter((day) => day.lessons !== lessons.length).map((day) => [day.day, day.lessons])),
  };
}

export function lessonsOn(plan: ShiftPlan, day: number): number {
  return Math.min(plan.dayLessons[day] ?? plan.lessonCount, plan.lessonCount);
}

export function weeklyLessons(plan: ShiftPlan, days: readonly number[]): number {
  return days.reduce((sum, day) => sum + lessonsOn(plan, day), 0);
}

/** The request body for one shift: breaks beyond the last lesson are dropped; only working days are sent. */
export function toShiftInput(kind: "morning" | "evening", plan: ShiftPlan, days: readonly number[]): WizardShiftInput {
  return {
    kind,
    firstStartTime: plan.firstStartTime,
    lessonMinutes: plan.lessonMinutes,
    lessonCount: plan.lessonCount,
    breaks: plan.breaks.filter((slot) => slot.afterLesson < plan.lessonCount),
    dayLessons: days.map((day) => ({ day, lessons: lessonsOn(plan, day) })),
  };
}
