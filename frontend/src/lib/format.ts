import { messages } from "../i18n/messages";

/**
 * The single formatting helper (DESIGN_SYSTEM.md section 8). Every number, date and time shown in the UI goes
 * through here, following the school's numerals, calendar and time-zone settings (defaults before setup).
 */
export type NumeralSystem = "arab" | "latn";
export type NumeralPreference = "arabicIndic" | "western";
export type CalendarPreference = "gregorian" | "hijri";

export type DisplayPreferences = {
  numeralSystem: NumeralPreference;
  calendarDisplay: CalendarPreference;
  timeZone: string;
};

export const defaultDisplay: DisplayPreferences = {
  numeralSystem: "arabicIndic",
  calendarDisplay: "gregorian",
  timeZone: "Asia/Baghdad",
};

export const defaultNumeralSystem: NumeralSystem = "arab";

const numberFormatters = new Map<NumeralSystem, Intl.NumberFormat>();

export function numeralSystemOf(preference: NumeralPreference): NumeralSystem {
  return preference === "western" ? "latn" : "arab";
}

export function formatNumber(value: number, system: NumeralSystem = defaultNumeralSystem): string {
  let formatter = numberFormatters.get(system);
  if (!formatter) {
    formatter = new Intl.NumberFormat("ar", { numberingSystem: system, useGrouping: false });
    numberFormatters.set(system, formatter);
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
export function formatInactivityTimeout(minutes: number | null, system: NumeralSystem = defaultNumeralSystem): string {
  return minutes === null ? messages.app.neverLock : formatMinutes(minutes, system);
}

/** Parses an API date ("yyyy-MM-dd") as a calendar date (UTC midnight, so no time-zone shift). */
export function parseApiDate(value: string): Date {
  return new Date(`${value}T00:00:00Z`);
}

/** Today's calendar date in the school's time zone, as "yyyy-MM-dd". */
export function todayIn(timeZone: string, now: Date = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone, year: "numeric", month: "2-digit", day: "2-digit" }).format(now);
}

export type Formatter = ReturnType<typeof createFormatter>;

export function createFormatter(preferences: DisplayPreferences = defaultDisplay) {
  const system = numeralSystemOf(preferences.numeralSystem);
  const calendar = preferences.calendarDisplay === "hijri" ? "islamic-umalqura" : "gregory";
  const dateFormat = new Intl.DateTimeFormat("ar", {
    calendar, numberingSystem: system, timeZone: "UTC", day: "numeric", month: "long", year: "numeric",
  });
  const monthFormat = new Intl.DateTimeFormat("ar", {
    calendar, numberingSystem: system, timeZone: "UTC", month: "long", year: "numeric",
  });
  const timeFormat = new Intl.DateTimeFormat("ar", {
    numberingSystem: system, timeZone: "UTC", hour: "2-digit", minute: "2-digit", hourCycle: "h23",
  });
  return {
    preferences,
    number: (value: number) => formatNumber(value, system),
    minutes: (value: number) => formatMinutes(value, system),
    inactivity: (value: number | null) => formatInactivityTimeout(value, system),
    date: (value: string) => dateFormat.format(parseApiDate(value)),
    dateRange: (start: string, end: string) => `${dateFormat.format(parseApiDate(start))} – ${dateFormat.format(parseApiDate(end))}`,
    month: (value: string) => monthFormat.format(parseApiDate(value)),
    time: (value: string) => timeFormat.format(new Date(`1970-01-01T${value}:00Z`)),
    today: () => todayIn(preferences.timeZone),
  };
}
