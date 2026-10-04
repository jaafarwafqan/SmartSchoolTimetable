import { describe, expect, it } from "vitest";
import { isolate } from "../i18n/isolate";
import { messages } from "../i18n/messages";
import { createFormatter, formatInactivityTimeout, formatMinutes, formatNumber, todayIn } from "./format";

describe("formatting helper", () => {
  it("renders numbers in the chosen numeral system", () => {
    expect(formatNumber(2027, "arab")).toBe("٢٠٢٧");
    expect(formatNumber(2027, "latn")).toBe("2027");
    expect(createFormatter({ numeralSystem: "western", calendarDisplay: "gregorian", timeZone: "UTC" }).number(45)).toBe("45");
    expect(createFormatter().number(45)).toBe("٤٥");
  });

  it("uses Arabic grammatical number for minutes", () => {
    expect(formatMinutes(1)).toBe(messages.app.minutesOne);
    expect(formatMinutes(2)).toBe(messages.app.minutesTwo);
    expect(formatMinutes(5)).toBe(messages.app.minutesFew("٥"));
    expect(formatMinutes(30, "latn")).toBe(messages.app.minutesMany("30"));
    expect(formatInactivityTimeout(null)).toBe(messages.app.neverLock);
  });

  it("formats API dates without time-zone drift in Gregorian and Hijri calendars", () => {
    const gregorian = createFormatter({ numeralSystem: "western", calendarDisplay: "gregorian", timeZone: "Asia/Baghdad" });
    expect(gregorian.date("2026-09-01")).toContain("2026");
    expect(gregorian.date("2026-09-01")).toContain("1");
    const hijri = createFormatter({ numeralSystem: "western", calendarDisplay: "hijri", timeZone: "Asia/Baghdad" });
    expect(hijri.date("2026-09-01")).toContain("1448");
    expect(gregorian.dateRange("2026-09-01", "2027-06-30")).toContain("–");
  });

  it("formats lesson times on a 24-hour clock with the chosen digits", () => {
    expect(createFormatter({ numeralSystem: "western", calendarDisplay: "gregorian", timeZone: "UTC" }).time("08:05")).toContain("08:05");
    expect(createFormatter().time("13:40")).toContain("١٣");
  });

  it("computes today in the school time zone", () => {
    const instant = new Date("2026-10-03T22:30:00Z");
    expect(todayIn("Asia/Baghdad", instant)).toBe("2026-10-04");
    expect(todayIn("UTC", instant)).toBe("2026-10-03");
  });

  it("isolates user values embedded in Arabic sentences", () => {
    expect(isolate("2026-2027")).toBe("⁨2026-2027⁩");
    expect(messages.school.years.termsOf("2026-2027")).toContain("⁨2026-2027⁩");
  });
});
