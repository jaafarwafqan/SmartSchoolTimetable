import { describe, expect, it } from "vitest";
import { ApiRequestError } from "../../i18n/errors";
import { messages } from "../../i18n/messages";
import { addMinutes, lessonNumbers, newRow, periodErrors } from "./periodRows";
import type { PeriodInput } from "./scheduleApi";

const lesson = (startTime: string, endTime: string): PeriodInput => ({ kind: "lesson", startTime, endTime, startBell: true, endBell: true });

describe("period rows", () => {
  it("maps row-level and list-level API errors to Arabic messages", () => {
    const error = new ApiRequestError("VALIDATION_FAILED", [
      { field: "Periods[1].StartTime", code: "PERIODS_OVERLAP" },
      { field: "Periods[1].EndTime", code: "INVALID_TIME_RANGE" },
      { field: "Periods", code: "NO_LESSON_PERIODS" },
    ]);
    expect(periodErrors(error)).toEqual({
      rows: { 1: { startTime: messages.errors.PERIODS_OVERLAP, endTime: messages.errors.INVALID_TIME_RANGE } },
      list: messages.errors.NO_LESSON_PERIODS,
    });
    expect(periodErrors(new ApiRequestError("CONFLICT"))).toBeNull();
    expect(periodErrors(new Error("network"))).toBeNull();
  });

  it("numbers lessons in order and skips breaks", () => {
    const rows = [lesson("08:00", "08:45"), { ...lesson("08:45", "09:00"), kind: "break" as const }, lesson("09:00", "09:45")];
    expect(lessonNumbers(rows)).toEqual([1, null, 2]);
  });

  it("adds rows after the last one without passing midnight", () => {
    expect(newRow([], "lesson")).toEqual(lesson("08:00", "08:45"));
    expect(newRow([lesson("08:00", "08:45")], "break")).toEqual({ kind: "break", startTime: "08:45", endTime: "09:00", startBell: false, endBell: false });
    expect(addMinutes("23:50", 45)).toBe("23:59");
  });
});
