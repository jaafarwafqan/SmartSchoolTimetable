import { describe, expect, it } from "vitest";
import { applyLevel, levelOf, levelWeights, priorityLevels, ruleKeys, withLevel, type RuleInput } from "./profileApi";

const rule = (enabled: boolean, weight: number): RuleInput => ({ key: "avoidTeacherGaps", enabled, weight });

describe("priority levels (MF9)", () => {
  it("maps the three levels to documented weights", () => {
    expect(priorityLevels).toEqual(["notImportant", "important", "veryImportant"]);
    expect(levelWeights).toEqual({ notImportant: 0, important: 20, veryImportant: 40 });
    // «مهم جداً» weighs 80 for spreading a subject across the days only (owner, ADR 0055).
    expect(withLevel({ key: "spreadSubjectsAcrossDays", enabled: true, weight: 20 }, "veryImportant")).toEqual({ key: "spreadSubjectsAcrossDays", enabled: true, weight: 80 });
    expect(withLevel({ key: "balanceTeacherDays", enabled: true, weight: 20 }, "veryImportant").weight).toBe(40);
    expect(levelOf({ enabled: true, weight: 80 })).toBe("veryImportant");
  });

  it("reads a saved rule as the nearest level, and the default weights come out as the owner set them", () => {
    expect(levelOf(rule(false, 40))).toBe("notImportant");
    expect(levelOf(rule(true, 0))).toBe("notImportant");
    expect(levelOf(rule(true, 9))).toBe("notImportant");
    expect([10, 15, 20, 25, 29].map((weight) => levelOf(rule(true, weight)))).toEqual(Array(5).fill("important"));
    expect([30, 40, 100].map((weight) => levelOf(rule(true, weight)))).toEqual(Array(3).fill("veryImportant"));
    // Defaults: spread 20, gaps 30, heavy 15, repeated 25, doubles 10.
    expect([20, 30, 15, 25, 10].map((weight) => levelOf(rule(true, weight)))).toEqual(["important", "veryImportant", "important", "important", "important"]);
  });

  it("reads the new defaults as the owner set them: spread with gaps and balanced days very important, fair last periods important, consecutive off", () => {
    // Defaults in display order: spread subjects 20, spread teacher 30, consecutive (off) 30, balance 40, fair last periods 20, heavy 15, repeated 25, doubles 10.
    expect(ruleKeys).toHaveLength(9);
    expect(levelOf({ enabled: true, weight: 30 })).toBe("veryImportant");
    expect(levelOf({ enabled: true, weight: 40 })).toBe("veryImportant");
    expect(levelOf({ enabled: true, weight: 20 })).toBe("important");
    expect(levelOf({ enabled: false, weight: 30 })).toBe("notImportant");
  });

  it("switching one form of the teacher's day on switches the other off, and only those two exclude each other", () => {
    const rules: RuleInput[] = [
      { key: "spreadTeacherLessons", enabled: true, weight: 30 },
      { key: "avoidTeacherGaps", enabled: false, weight: 30 },
      { key: "balanceTeacherDays", enabled: true, weight: 40 },
    ];
    const consecutive = applyLevel(rules, "avoidTeacherGaps", "important");
    expect(consecutive.find((item) => item.key === "avoidTeacherGaps")).toEqual({ key: "avoidTeacherGaps", enabled: true, weight: 20 });
    expect(consecutive.find((item) => item.key === "spreadTeacherLessons")).toEqual({ key: "spreadTeacherLessons", enabled: false, weight: 0 });
    expect(consecutive.find((item) => item.key === "balanceTeacherDays")).toBe(rules[2]);
    // Switching one off leaves the other as it is.
    const off = applyLevel(rules, "spreadTeacherLessons", "notImportant");
    expect(off.find((item) => item.key === "avoidTeacherGaps")).toBe(rules[1]);
  });

  it("rewrites a rule only when its level changes", () => {
    const saved = rule(true, 25);
    expect(withLevel(saved, "important")).toBe(saved);
    expect(withLevel(saved, "veryImportant")).toEqual(rule(true, 40));
    expect(withLevel(saved, "notImportant")).toEqual(rule(false, 0));
    expect(withLevel(rule(false, 0), "important")).toEqual(rule(true, 20));
  });
});
