import { messages } from "../i18n/messages";

/**
 * The ONE place times become text for people (R1, Iraqi convention): 12-hour clock with «ص» (before noon) and «م»
 * (from noon). Storage, the API and the database keep 24-hour "HH:mm"; convert only here, at the UI boundary.
 * 00:00 → «١٢:٠٠ ص», 12:00 → «١٢:٠٠ م», 13:05 → «١:٠٥ م». Hours have no leading zero; minutes always two digits.
 */
export type Meridiem = "am" | "pm";

export type Time12 = { hour: number; minute: number; meridiem: Meridiem };

const arabicIndic = ["٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩"];

function digitsOf(text: string, arabic: boolean): string {
  return arabic ? text.replace(/[0-9]/g, (digit) => arabicIndic[Number(digit)]) : text;
}

/** "HH:mm" (24-hour) → hour 1–12, minute, ص/م; null when the text is not a valid time. */
export function parseTime24(value: string | null | undefined): Time12 | null {
  const match = /^(\d{1,2}):(\d{2})$/.exec(value ?? "");
  if (!match) return null;
  const hours = Number(match[1]);
  const minute = Number(match[2]);
  if (hours > 23 || minute > 59) return null;
  return { hour: hours % 12 === 0 ? 12 : hours % 12, minute, meridiem: hours < 12 ? "am" : "pm" };
}

/** Hour 1–12, minute, ص/م → "HH:mm" (24-hour) for the API. */
export function toTime24({ hour, minute, meridiem }: Time12): string {
  const hours = (hour % 12) + (meridiem === "pm" ? 12 : 0);
  return `${String(hours).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

/** "HH:mm" → «٨:٠٠ ص» with the school's numerals ("arab" Arabic-Indic, "latn" Western). Invalid input is returned as is. */
export function formatTime12(value: string, numerals: "arab" | "latn" = "arab"): string {
  const time = parseTime24(value);
  if (!time) return value;
  const marker = time.meridiem === "am" ? messages.app.timeParts.am : messages.app.timeParts.pm;
  return `${digitsOf(`${time.hour}:${String(time.minute).padStart(2, "0")}`, numerals === "arab")} ${marker}`;
}

/** Minutes after midnight → "HH:mm". */
export function clockOf(minutes: number): string {
  const total = ((minutes % 1440) + 1440) % 1440;
  return `${String(Math.floor(total / 60)).padStart(2, "0")}:${String(total % 60).padStart(2, "0")}`;
}
