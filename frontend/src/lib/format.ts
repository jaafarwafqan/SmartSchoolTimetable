import { arabicCount, type CountNoun, type GrammaticalCase } from "./arabicCount";
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
  return arabicCount(minutes, "minute", (value) => formatNumber(value, system));
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

/**
 * Gregorian month names as used in Iraq (DECISIONS_PENDING #31): كانون الثاني … كانون الأول. `ar-IQ` yields them;
 * this table also replaces the month part if an engine lacks that locale's data. Hijri months are unchanged.
 */
export const iraqiMonthNames = [
  "كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران",
  "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول",
] as const;

function withIraqiMonths(format: Intl.DateTimeFormat, gregorian: boolean) {
  return (date: Date) => format.formatToParts(date)
    .map((part) => (gregorian && part.type === "month" ? iraqiMonthNames[date.getUTCMonth()] : part.value))
    .join("");
}

export function createFormatter(preferences: DisplayPreferences = defaultDisplay) {
  const system = numeralSystemOf(preferences.numeralSystem);
  const calendar = preferences.calendarDisplay === "hijri" ? "islamic-umalqura" : "gregory";
  const gregorian = calendar === "gregory";
  const locale = gregorian ? "ar-IQ" : "ar";
  const dateFormat = new Intl.DateTimeFormat(locale, {
    calendar, numberingSystem: system, timeZone: "UTC", day: "numeric", month: "long", year: "numeric",
  });
  const monthFormat = new Intl.DateTimeFormat(locale, {
    calendar, numberingSystem: system, timeZone: "UTC", month: "long", year: "numeric",
  });
  const timeFormat = new Intl.DateTimeFormat("ar", {
    numberingSystem: system, timeZone: "UTC", hour: "2-digit", minute: "2-digit", hourCycle: "h23",
  });
  const formatDate = withIraqiMonths(dateFormat, gregorian);
  const formatMonth = withIraqiMonths(monthFormat, gregorian);
  return {
    preferences,
    number: (value: number) => formatNumber(value, system),
    /** A counted noun with Arabic agreement and the school's numerals ("٩ مواد"، "مادتان"). */
    count: (value: number, noun: CountNoun, grammaticalCase?: GrammaticalCase) =>
      arabicCount(value, noun, (number) => formatNumber(number, system), grammaticalCase),
    minutes: (value: number) => formatMinutes(value, system),
    inactivity: (value: number | null) => formatInactivityTimeout(value, system),
    date: (value: string) => formatDate(parseApiDate(value)),
    dateRange: (start: string, end: string) => `${formatDate(parseApiDate(start))} – ${formatDate(parseApiDate(end))}`,
    month: (value: string) => formatMonth(parseApiDate(value)),
    time: (value: string) => timeFormat.format(new Date(`1970-01-01T${value}:00Z`)),
    today: () => todayIn(preferences.timeZone),
  };
}
