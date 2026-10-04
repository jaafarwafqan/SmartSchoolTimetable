import { ApiRequestError, fieldErrorMessage } from "../../i18n/errors";
import type { PeriodInput, PeriodKind } from "./scheduleApi";

export type PeriodField = "kind" | "startTime" | "endTime";
export type PeriodRowErrors = Record<number, Partial<Record<PeriodField, string>>>;
export type PeriodErrors = { rows: PeriodRowErrors; list: string | null };

const rowField = /^Periods\[(\d+)\]\.(Kind|StartTime|EndTime)$/;
const fieldNames: Record<string, PeriodField> = { Kind: "kind", StartTime: "startTime", EndTime: "endTime" };

/** Splits API validation errors into per-row messages ("Periods[i].StartTime") and a list-level message. */
export function periodErrors(reason: unknown): PeriodErrors | null {
  if (!(reason instanceof ApiRequestError) || reason.fields.length === 0) return null;
  const rows: PeriodRowErrors = {};
  let list: string | null = null;
  for (const { field, code } of reason.fields) {
    const match = rowField.exec(field);
    if (match) {
      const row = Number(match[1]);
      rows[row] = { ...rows[row], [fieldNames[match[2]]]: fieldErrorMessage(code) };
    } else if (field === "Periods") {
      list ??= fieldErrorMessage(code);
    }
  }
  return { rows, list };
}

/** Lesson numbers (1..N) in row order; breaks have no number (DECISIONS_PENDING #4). */
export function lessonNumbers(rows: readonly PeriodInput[]): (number | null)[] {
  let lesson = 0;
  return rows.map((row) => (row.kind === "lesson" ? ++lesson : null));
}

/** "HH:mm" plus minutes, capped at 23:59 so a new row never wraps past midnight. */
export function addMinutes(time: string, minutes: number): string {
  const [hours, mins] = time.split(":").map(Number);
  const total = Math.min((hours || 0) * 60 + (mins || 0) + minutes, 23 * 60 + 59);
  return `${String(Math.floor(total / 60)).padStart(2, "0")}:${String(total % 60).padStart(2, "0")}`;
}

/** A row that starts when the last row ends (or at 08:00): 45-minute lessons, 15-minute breaks. */
export function newRow(rows: readonly PeriodInput[], kind: PeriodKind): PeriodInput {
  const start = rows.at(-1)?.endTime || "08:00";
  const lesson = kind === "lesson";
  return { kind, startTime: start, endTime: addMinutes(start, lesson ? 45 : 15), startBell: lesson, endBell: lesson };
}
