import type { PeriodPreset } from "../curriculum/curriculumApi";
import { planFromPreset, planFromShift, toShiftInput, type ShiftPlan } from "../setup-wizard/timingPlan";
import type { WizardShiftInput } from "../setup-wizard/wizardApi";
import type { Shift } from "./scheduleApi";
import { mappingOf, morningDays, reversedMorning, timingForm, type SessionDay, type SessionPlan } from "./sessionPlan";

/** «نظام الدوام» (MF7): one shift in every case; «مزدوج» alternates a morning and an evening session by day. */
export type ShiftSystem = "morning" | "evening" | "dual";
export type TimingSession = "morning" | "evening";

export type ShiftSystemValue = {
  system: ShiftSystem;
  /** The shift's own timing: morning for «صباحي» and «مزدوج», evening for «مسائي». */
  main: ShiftPlan;
  /** «مزدوج» only: the evening session (its lesson count is always the main one). */
  evening: ShiftPlan;
  /** «مزدوج» only: the morning days of each semester; the other working days are evening. */
  morningDays: Record<1 | 2, number[]>;
};

export const defaultStart: Record<TimingSession, string> = { morning: "08:00", evening: "13:00" };

/** «بدون قالب (أبدأ من الصفر)»: no breaks, plain defaults. It is the first option and the default (MF1). */
export const noPreset = "";

/** The templates offered for a session: the evening template never appears in the morning list, and the reverse (MF1). */
export function presetsFor(presets: readonly PeriodPreset[], session: TimingSession): PeriodPreset[] {
  return presets.filter((preset) => (preset.session ?? "morning") === session);
}

/** A fresh plan for a session: «بدون قالب» unless a template is chosen. */
export function blankPlan(session: TimingSession, lessonCount = 6, lessonMinutes = 45): ShiftPlan {
  return { ...planFromPreset(undefined, defaultStart[session]), lessonCount, lessonMinutes };
}

/** Applies a template (or «بدون قالب») to a plan, keeping the per-day counts the owner set. */
export function applyPreset(plan: ShiftPlan, presets: readonly PeriodPreset[], key: string, session: TimingSession): ShiftPlan {
  const preset = presets.find((item) => item.key === key);
  if (!preset) return { ...plan, presetKey: noPreset, breaks: [], gapMinutes: 0 };
  return { ...planFromPreset(preset, preset.firstStart || defaultStart[session]), dayLessons: plan.dayLessons };
}

/** The first two working days are morning in semester 1, the rest evening; semester 2 is the reverse. */
export function defaultMorningDays(workingDays: readonly number[]): Record<1 | 2, number[]> {
  const first = workingDays.slice(0, Math.ceil(workingDays.length / 2));
  return { 1: first, 2: reversedMorning(first, workingDays) };
}

/** Builds the editor's starting value from what is stored (system, the year's shift, the session plan). */
export function initialValue(system: ShiftSystem, shift: Shift | undefined, plan: SessionPlan | undefined, workingDays: readonly number[]): ShiftSystemValue {
  const mainSession: TimingSession = system === "evening" ? "evening" : "morning";
  const main = (shift && planFromShift(shift)) ?? blankPlan(mainSession);
  const eveningRows = plan?.timings.find((timing) => timing.session === "evening")?.periods;
  const form = timingForm(eveningRows, defaultStart.evening, main.lessonMinutes);
  const evening: ShiftPlan = { ...blankPlan("evening", main.lessonCount, form.lessonMinutes), firstStartTime: form.firstStart, breaks: form.breaks, gapMinutes: form.gapMinutes };
  const mapped = plan && plan.system === "twoSessions" && plan.days.length > 0
    ? { 1: morningDays(plan.days, 1, workingDays), 2: morningDays(plan.days, 2, workingDays) }
    : defaultMorningDays(workingDays);
  return { system, main, evening, morningDays: mapped };
}

export type ShiftSystemCommand = {
  system: ShiftSystem;
  main: WizardShiftInput;
  evening?: WizardShiftInput;
  sessionDays?: SessionDay[];
};

/** The request body (shift-system or the wizard's timing step) for the chosen working days. */
export function toCommand(value: ShiftSystemValue, days: readonly number[]): ShiftSystemCommand {
  const main = toShiftInput(value.system === "evening" ? "evening" : "morning", value.main, days);
  if (value.system !== "dual") return { system: value.system, main };
  const evening = toShiftInput("evening", { ...value.evening, lessonCount: value.main.lessonCount, dayLessons: {} }, days);
  const morning = { 1: value.morningDays[1].filter((day) => days.includes(day)), 2: value.morningDays[2].filter((day) => days.includes(day)) };
  return { system: "dual", main, evening, sessionDays: mappingOf(morning, days) };
}
