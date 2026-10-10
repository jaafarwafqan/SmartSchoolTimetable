import { describe, expect, it } from "vitest";
import { messages } from "../../i18n/messages";
import { createFormatter } from "../../lib/format";
import type { AuditEntry } from "./historyApi";
import { auditSentence, entryTime, localDateTime } from "./historyPresentation";

const format = createFormatter({ numeralSystem: "arabicIndic", calendarDisplay: "gregorian", timeZone: "Asia/Baghdad" });
const latin = createFormatter({ numeralSystem: "western", calendarDisplay: "gregorian", timeZone: "Asia/Baghdad" });
const text = messages.school.audit;

const entry = (eventType: string, params: AuditEntry["params"] = null): AuditEntry => ({
  id: 1, occurredAt: "2026-10-10T12:30:00+00:00", eventType, category: "timetable", target: "timetable:1", params,
});

describe("audit sentences", () => {
  it("builds the sentence from the code and the parameters in the school's numerals", () => {
    expect(auditSentence(entry("TimetableApproved", { number: 3 }), format)).toBe("اعتُمد الإصدار ٣.");
    expect(auditSentence(entry("TimetableApproved", { number: 3 }), latin)).toBe("اعتُمد الإصدار 3.");
    expect(auditSentence(entry("TimetableRolledBack", { number: 5, from: 2 }), format)).toBe("استُرجع الإصدار ٢ كإصدار جديد رقم ٥.");
  });

  it("degrades gracefully for entries without parameters", () => {
    expect(auditSentence(entry("TimetableApproved"), format)).toBe("اعتُمد الإصدار.");
    expect(auditSentence(entry("BackupCreated"), format)).toBe(text.events.BackupCreated());
  });

  it("turns known codes into Arabic names and never shows an unknown code", () => {
    expect(auditSentence(entry("GenerationFinished", { status: "timedOut" }), format)).toBe("انتهى التوليد: انتهت المهلة.");
    expect(auditSentence(entry("GenerationFinished", { status: "somethingNew" }), format)).toBe("انتهى التوليد.");
  });

  it("leaves out zero counts, reads booleans, and keeps unknown events generic", () => {
    expect(auditSentence(entry("GenerationStarted", { mode: "standard", locked: 0 }), format)).toBe("بدأ توليد جدول (عادي).");
    expect(auditSentence(entry("GenerationStarted", { mode: "standard", locked: 4 }), format)).toBe("بدأ توليد جدول (عادي)، الحصص اليدوية المُبقاة في مكانها: ٤.");
    expect(auditSentence(entry("TimetableArchived", { number: 1, byApproval: true }), format)).toContain("لأن إصداراً آخر اعتُمد");
    expect(auditSentence(entry("TimetableArchived", { number: 1, byApproval: false }), format)).toBe("أُرشف الإصدار ١.");
    expect(auditSentence(entry("EventFromAFutureBuild"), format)).toBe(text.unknownEvent);
  });
});

describe("history time", () => {
  it("shows the school's local date and 12-hour clock, not UTC", () => {
    expect(localDateTime("2026-10-10T21:30:00Z", "Asia/Baghdad")).toEqual({ date: "2026-10-11", time: "00:30" });
    expect(entryTime(entry("BackupCreated"), latin)).toBe(text.at(latin.date("2026-10-10"), "3:30 م"));
  });
});
