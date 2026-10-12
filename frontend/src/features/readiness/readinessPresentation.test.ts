import { describe, expect, it } from "vitest";
import { isolate } from "../../i18n/isolate";
import { createFormatter } from "../../lib/format";
import { errorCount, findingMessage, groupFindings, warningCount } from "./readinessPresentation";
import { messages } from "../../i18n/messages";
import { findingCodes, type ReadinessFinding } from "./readinessApi";

const format = createFormatter();

function finding(overrides: Partial<ReadinessFinding> = {}): ReadinessFinding {
  return {
    code: "TEACHER_OVERLOAD", severity: "error", entity: { kind: "teacher", id: 4, name: "أحمد" }, related: [],
    required: 28, available: 25, shortage: 3, details: [], fixes: ["moveWorkload"], ...overrides,
  };
}

describe("readiness findings", () => {
  it("renders the teacher shortage with the required Arabic numbers and noun forms", () => {
    expect(findingMessage(finding(), format)).toBe(`المعلم ${isolate("أحمد")}: المطلوب ٢٨ حصة، المتاح ٢٥، ينقص ٣ حصص.`);
  });

  it("says which section, subject, teacher and day the fill checks mean (the owner's real-school case)", () => {
    const section = { kind: "section", id: 1, name: "الأول / أ" };
    expect(findingMessage(finding({
      code: "SUBJECT_DAYS_SHORT", entity: section, related: [{ kind: "subject", id: 2, name: "العربي" }, { kind: "teacher", id: 3, name: "رغد" }],
      required: 5, available: 4, shortage: 1, details: ["10", "2", "7"],
    }), format)).toBe(`${isolate("العربي")} للشعبة ${isolate("الأول / أ")} يحتاج ٥ أيام (١٠ حصص، والحد اليومي حصتان) والمعلم ${isolate("رغد")} متاح ٤ أيام فقط (غير متاح: الأحد).`);
    expect(findingMessage(finding({
      code: "SECTION_DAY_UNDERFILLED", entity: section, related: [{ kind: "teacher", id: 3, name: "سلمى" }], required: 3, available: 2, shortage: 1, details: ["7", "3"],
    }), format)).toContain(`يوم الأحد`);
    expect(findingMessage(finding({ code: "SECTION_SLOT_UNCOVERED", entity: section, required: 1, available: 0, shortage: 1, details: ["1", "2"] }), format))
      .toContain("لا يوجد أي معلم متاح يوم الاثنين في الحصة ٢");
    expect(findingMessage(finding({
      code: "TEACHERS_SAME_DAY_OFF", severity: "warning", entity: section, related: [{ kind: "teacher", id: 3, name: "رغد" }, { kind: "teacher", id: 4, name: "سلمى" }],
      required: 12, available: 11, shortage: 1, details: ["7", "13"],
    }), format)).toContain("إجازتهما يوم الأحد ويحملان ١٢ حصة من ١٣ حصة");
  });

  it("groups findings by entity and avoids double-counting teacher overload details", () => {
    const groups = groupFindings([
      finding(),
      finding({ code: "ASSIGNMENT_INFEASIBLE", required: 7, available: 5, shortage: 2 }),
      finding({ entity: { kind: "subject", id: 8, name: "الفيزياء" }, code: "SUBJECT_SLOTS_SHORT", required: 7, available: 5, shortage: 2 }),
    ]);
    expect(groups.map((group) => [group.entity.kind, group.findings.length, group.shortage])).toEqual([
      ["subject", 1, 2], ["teacher", 2, 3],
    ]);
  });

  it("uses the exact physics requirement and shortage", () => {
    expect(findingMessage(finding({ entity: { kind: "subject", id: 8, name: "الفيزياء" }, code: "SUBJECT_SLOTS_SHORT", required: 7, available: 5, shortage: 2 }), format))
      .toBe(`${isolate("الفيزياء")}: المطلوب ٧، المسموح ٥، ينقص ٢.`);
  });

  it("has an Arabic message for every finding code and none extra", () => {
    expect(Object.keys(messages.school.readiness.messages).sort()).toEqual([...findingCodes].sort());
    for (const code of findingCodes) {
      const text = findingMessage(finding({ code, entity: { kind: "teacher", id: 1, name: "س" }, details: ["الدوام الصباحي"] }), format);
      expect(text).not.toBe(messages.school.readiness.unknownFinding);
      expect(text).toMatch(/[\u0600-\u06FF]/);
    }
  });

  it("falls back to a generic Arabic message for an unknown code", () => {
    expect(findingMessage(finding({ code: "SOMETHING_NEW" }), format)).toBe(messages.school.readiness.unknownFinding);
  });

  it("says zero in words and agrees the counted nouns", () => {
    expect([0, 1, 2, 3, 11].map((value) => errorCount(value, format))).toEqual(["لا أخطاء", "خطأ واحد", "خطآن", "٣ أخطاء", "١١ خطأً"]);
    expect([0, 1, 2].map((value) => warningCount(value, format))).toEqual(["لا ملاحظات", "ملاحظة واحدة", "ملاحظتان"]);
  });

  it("explains that an impossible double only blocks the double-lesson mode when it is a warning", () => {
    const base = finding({ code: "DOUBLE_PERIOD_IMPOSSIBLE", entity: { kind: "subject", id: 9, name: "الفنية" }, required: 2, available: 1 });
    expect(findingMessage({ ...base, severity: "warning" }, format)).toContain(messages.school.readiness.doublePeriodWarningNote);
    expect(findingMessage({ ...base, severity: "error" }, format)).not.toContain(messages.school.readiness.doublePeriodWarningNote);
  });
});
