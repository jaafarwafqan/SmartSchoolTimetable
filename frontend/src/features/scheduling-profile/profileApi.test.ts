import { describe, expect, it } from "vitest";
import { levelOf, levelWeights, priorityLevels, withLevel, type RuleInput } from "./profileApi";

const rule = (enabled: boolean, weight: number): RuleInput => ({ key: "avoidTeacherGaps", enabled, weight });

describe("priority levels (MF9)", () => {
  it("maps the three levels to documented weights", () => {
    expect(priorityLevels).toEqual(["notImportant", "important", "veryImportant"]);
    expect(levelWeights).toEqual({ notImportant: 0, important: 20, veryImportant: 40 });
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

  it("rewrites a rule only when its level changes", () => {
    const saved = rule(true, 25);
    expect(withLevel(saved, "important")).toBe(saved);
    expect(withLevel(saved, "veryImportant")).toEqual(rule(true, 40));
    expect(withLevel(saved, "notImportant")).toEqual(rule(false, 0));
    expect(withLevel(rule(false, 0), "important")).toEqual(rule(true, 20));
  });
});
