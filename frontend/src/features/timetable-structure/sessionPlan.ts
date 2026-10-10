import { clockOf, minutesOf } from "../../lib/time";
import { validBreaks, type BreakSlot } from "./BreaksEditor";
import type { PeriodInput, PeriodKind } from "./scheduleApi";

export type SessionSystem = "oneSession" | "twoSessions" | "threeSessions";
export type SessionKind = "morning" | "noon" | "evening";
export type SessionPeriod = { position: number; kind: PeriodKind; startTime: string; endTime: string };
export type SessionTiming = { session: SessionKind; periods: SessionPeriod[] };
export type SessionDay = { term: 1 | 2; day: number; session: SessionKind };
export type SessionPlan = {
  system: SessionSystem;
  shiftId: number | null;
  available: boolean;
  lessonCount: number;
  workingDays: number[];
  timings: SessionTiming[];
  days: SessionDay[];
  version: number;
};

/** The form of a session's timing: everything is chosen from controls, then turned into period rows. */
export type TimingForm = { firstStart: string; lessonMinutes: number; breaks: BreakSlot[]; gapMinutes: number };

export const minLessonMinutes = 10;
export const maxLessonMinutes = 120;

/** Lesson and break rows of a session (the same rule as the server's period generator). */
export function buildPeriods(form: TimingForm, lessonCount: number): Omit<PeriodInput, "startBell" | "endBell">[] {
  let time = minutesOf(form.firstStart);
  if (time === null || lessonCount < 1) return [];
  const breaks = validBreaks(lessonCount, form.breaks);
  const rows: Omit<PeriodInput, "startBell" | "endBell">[] = [];
  for (let lesson = 1; lesson <= lessonCount; lesson++) {
    rows.push({ kind: "lesson", startTime: clockOf(time), endTime: clockOf(time + form.lessonMinutes) });
    time += form.lessonMinutes;
    if (lesson === lessonCount) break;
    const slot = breaks.find((item) => item.afterLesson === lesson);
    if (slot) {
      rows.push({ kind: "break", startTime: clockOf(time), endTime: clockOf(time + slot.minutes) });
      time += slot.minutes;
    } else {
      time += form.gapMinutes;
    }
  }
  return rows;
}

/** Reads a saved timing back into the form (first start, first lesson's length, breaks and the gap between lessons). */
export function timingForm(periods: readonly SessionPeriod[] | undefined, fallbackStart: string, fallbackMinutes: number): TimingForm {
  const rows = [...(periods ?? [])].sort((a, b) => a.position - b.position);
  const lessons = rows.filter((row) => row.kind === "lesson");
  const first = lessons[0];
  if (!first) return { firstStart: fallbackStart, lessonMinutes: fallbackMinutes, breaks: [], gapMinutes: 0 };
  const minutes = (minutesOf(first.endTime) ?? 0) - (minutesOf(first.startTime) ?? 0);
  const breaks: BreakSlot[] = [];
  let gapMinutes = 0;
  let lesson = 0;
  rows.forEach((row, index) => {
    if (row.kind === "lesson") {
      lesson += 1;
      const next = rows[index + 1];
      if (next?.kind === "lesson") gapMinutes = Math.max(0, (minutesOf(next.startTime) ?? 0) - (minutesOf(row.endTime) ?? 0));
      return;
    }
    breaks.push({ afterLesson: lesson, minutes: (minutesOf(row.endTime) ?? 0) - (minutesOf(row.startTime) ?? 0) });
  });
  return { firstStart: first.startTime, lessonMinutes: Math.max(minLessonMinutes, Math.min(maxLessonMinutes, minutes)), breaks, gapMinutes };
}

/** Morning days of a semester (the remaining working days are evening in a double system). */
export function morningDays(days: readonly SessionDay[], term: 1 | 2, workingDays: readonly number[]): number[] {
  const mapped = days.filter((item) => item.term === term);
  if (mapped.length === 0) return [...workingDays];
  return workingDays.filter((day) => mapped.find((item) => item.day === day)?.session !== "evening");
}

/** The double-system mapping of both semesters from their morning days. */
export function mappingOf(morning: Record<1 | 2, readonly number[]>, workingDays: readonly number[]): SessionDay[] {
  return ([1, 2] as const).flatMap((term) => workingDays.map((day) => ({ term, day, session: morning[term].includes(day) ? "morning" as const : "evening" as const })));
}

/** «اعكس للفصل الثاني»: semester 2's morning days are semester 1's evening days. */
export const reversedMorning = (firstMorning: readonly number[], workingDays: readonly number[]) => workingDays.filter((day) => !firstMorning.includes(day));
