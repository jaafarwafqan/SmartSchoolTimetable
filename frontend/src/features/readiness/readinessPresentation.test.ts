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
});
