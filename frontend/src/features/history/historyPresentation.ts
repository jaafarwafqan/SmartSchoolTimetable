import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import type { AuditEntry } from "./historyApi";

const text = messages.school.audit;

type Template = (parameters: Record<string, string | undefined>) => string;

/**
 * The Arabic sentence of a history entry. Numbers use the school's numerals; known codes (status, mode, system) become their
 * Arabic names; zero counts and unknown codes are left out, and an unknown event shows a generic sentence (never a raw code).
 */
export function auditSentence(entry: AuditEntry, format: Formatter): string {
  const template = (text.events as Record<string, Template | undefined>)[entry.eventType];
  if (!template) return text.unknownEvent;
  const labels = text.paramLabels as Record<string, Record<string, string> | undefined>;
  const parameters: Record<string, string | undefined> = {};
  for (const [key, value] of Object.entries(entry.params ?? {})) {
    if (typeof value === "number") parameters[key] = value === 0 ? undefined : format.number(value);
    else if (typeof value === "boolean") parameters[key] = value ? "1" : undefined;
    else parameters[key] = labels[key]?.[value];
  }
  return template(parameters);
}

/** The school's local calendar date and clock of a moment (the API stores UTC). */
export function localDateTime(iso: string, timeZone: string): { date: string; time: string } {
  const parts = new Intl.DateTimeFormat("en-CA", { timeZone, year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23" })
    .formatToParts(new Date(iso));
  const get = (type: string) => parts.find((part) => part.type === type)?.value ?? "00";
  return { date: `${get("year")}-${get("month")}-${get("day")}`, time: `${get("hour")}:${get("minute")}` };
}

/** «٥ شباط ٢٠٢٧، ٣:٠٠ م» */
export function entryTime(entry: AuditEntry, format: Formatter): string {
  const { date, time } = localDateTime(entry.occurredAt, format.preferences.timeZone);
  return text.at(format.date(date), format.time(time));
}
