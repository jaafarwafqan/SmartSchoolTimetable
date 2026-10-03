import { messages } from "../i18n/messages";

/**
 * The single numeral formatting helper (DESIGN_SYSTEM.md section 8). Every number shown in the UI goes through
 * here. The school-level numerals setting arrives in Phase 2; until then Arabic-Indic digits are the default.
 */
export type NumeralSystem = "arab" | "latn";

export const defaultNumeralSystem: NumeralSystem = "arab";

const formatters = new Map<NumeralSystem, Intl.NumberFormat>();

export function formatNumber(value: number, system: NumeralSystem = defaultNumeralSystem): string {
  let formatter = formatters.get(system);
  if (!formatter) {
    formatter = new Intl.NumberFormat("ar", { numberingSystem: system, useGrouping: false });
    formatters.set(system, formatter);
  }
  return formatter.format(value);
}

/** Arabic duration in minutes with correct grammatical number (1, 2, 3–10, 11+). */
export function formatMinutes(minutes: number, system: NumeralSystem = defaultNumeralSystem): string {
  if (minutes === 1) return messages.app.minutesOne;
  if (minutes === 2) return messages.app.minutesTwo;
  const count = formatNumber(minutes, system);
  return minutes >= 3 && minutes <= 10 ? messages.app.minutesFew(count) : messages.app.minutesMany(count);
}

/** Inactivity timeout label: a duration, or "never" when the value is null. */
export function formatInactivityTimeout(minutes: number | null): string {
  return minutes === null ? messages.app.neverLock : formatMinutes(minutes);
}
