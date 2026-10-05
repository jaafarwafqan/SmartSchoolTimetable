import { describe, expect, it } from "vitest";
import { parseLessons } from "./CurriculumGrid";

describe("curriculum cells", () => {
  it("parse weekly lessons in range, Arabic-Indic digits included, and empty as clear", () => {
    expect(parseLessons("٥")).toEqual({ ok: true, value: 5 });
    expect(parseLessons(" 15 ")).toEqual({ ok: true, value: 15 });
    expect(parseLessons("")).toEqual({ ok: true, value: null });
    expect(parseLessons("0").ok).toBe(false);
    expect(parseLessons("16").ok).toBe(false);
    expect(parseLessons("2.5").ok).toBe(false);
  });
});
