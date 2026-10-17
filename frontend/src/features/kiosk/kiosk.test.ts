import { describe, expect, it } from "vitest";
import { fileNameOf } from "../../api";
import { qrPath } from "../sharing/QrCode";
import { clockOf, kioskKey, lessonOf, minuteOfDay, momentAt, type KioskData } from "./kiosk";

const day: KioskData = {
  schoolName: "مدرسة الاختبار", date: "2026-11-01", timeZone: "Asia/Baghdad", status: "school", dayOffTitle: null, versionNumber: 3, semester: 1, session: 1,
  periods: [
    { number: 1, startMinute: 480, endMinute: 525 },
    { number: 2, startMinute: 530, endMinute: 575 },
    { number: null, startMinute: 575, endMinute: 590 },
    { number: 3, startMinute: 590, endMinute: 635 },
  ],
  sections: [{ index: 0, stageName: "الأول", label: "أ" }],
  lessons: [{ section: 0, lesson: 3, subject: "علوم", colorIndex: 3, teacher: "سارة" }],
};

describe("kiosk moments", () => {
  it("is closed with the reason on a day without school", () => {
    expect(momentAt({ ...day, status: "dayOff", dayOffTitle: "عطلة" }, 500)).toEqual({ kind: "closed", status: "dayOff", title: "عطلة" });
    expect(momentAt({ ...day, status: "noTimetable" }, 500)).toMatchObject({ kind: "closed", status: "noTimetable" });
  });

  it("says when the day starts, and that it is over after the last row", () => {
    expect(momentAt(day, 470)).toEqual({ kind: "beforeStart", startMinute: 480, next: day.periods[0] });
    expect(momentAt(day, 635)).toEqual({ kind: "dayOver" });
  });

  it("shows the current row (a lesson or a break) and the next LESSON", () => {
    expect(momentAt(day, 480)).toEqual({ kind: "open", current: day.periods[0], next: day.periods[1] });
    expect(momentAt(day, 580)).toEqual({ kind: "open", current: day.periods[2], next: day.periods[3] });
    expect(momentAt(day, 600)).toEqual({ kind: "open", current: day.periods[3], next: null });
  });

  it("treats a gap between two rows as the coming row", () => {
    // 525–530 has no row: the screen shows lesson 2 as current rather than nothing.
    expect(momentAt(day, 527)).toEqual({ kind: "open", current: day.periods[1], next: day.periods[3] });
  });

  it("finds a section's lesson by its number, never in a break", () => {
    expect(lessonOf(day, 0, 3)?.subject).toBe("علوم");
    expect(lessonOf(day, 0, 1)).toBeUndefined();
    expect(lessonOf(day, 0, null)).toBeUndefined();
  });
});

describe("kiosk clock and key", () => {
  it("reads minutes in the school's time zone, not the screen's", () => {
    // 10:00 UTC is 13:00 in Baghdad (UTC+3, no summer time).
    expect(minuteOfDay(new Date("2026-11-01T10:00:00Z"), "Asia/Baghdad")).toBe(13 * 60);
    expect(clockOf(13 * 60 + 5)).toBe("13:05");
  });

  it("takes the key from the address fragment and keeps it for a reload", () => {
    const store = new Map<string, string>();
    const storage = { getItem: (name: string) => store.get(name) ?? null, setItem: (name: string, value: string) => void store.set(name, value) };
    expect(kioskKey("#k=abcdefghijklmnopqrstuvwxyz012345", storage)).toBe("abcdefghijklmnopqrstuvwxyz012345");
    expect(kioskKey("", storage)).toBe("abcdefghijklmnopqrstuvwxyz012345");
    expect(kioskKey("#k=short", null)).toBeNull();
    expect(kioskKey("#k=<script>alert(1)</script>", null)).toBeNull();
  });
});

describe("sharing helpers", () => {
  it("draws each run of dark modules as one rectangle", () => {
    expect(qrPath(["0110", "1001"])).toBe("M1 0h2v1h-2zM0 1h1v1h-1zM3 1h1v1h-1z");
    expect(qrPath(["000"])).toBe("");
  });

  it("prefers the UTF-8 file name of a download (Arabic teacher names)", () => {
    expect(fileNameOf("attachment; filename=x.sst; filename*=UTF-8''%D8%A3%D8%AD%D9%85%D8%AF-v3.sst", "f.sst")).toBe("أحمد-v3.sst");
    expect(fileNameOf("attachment; filename=\"principal-v3.sst\"", "f.sst")).toBe("principal-v3.sst");
    expect(fileNameOf(null, "f.sst")).toBe("f.sst");
  });
});
