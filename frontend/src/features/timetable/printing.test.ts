import { describe, expect, it } from "vitest";
import { defaultOptions, fitScale, MIN_SCALE, MIN_SUBJECT_PT, MIN_TEACHER_PT, PRINT_BASE_PT, pageCss, planDays, printableWidth, printDefaultsFrom, resolvePrint, type PrintOptions } from "./printing";

const a4Landscape: PrintOptions = { scope: "current", paper: "A4", orientation: "landscape", fit: true, color: true };
const a3Landscape: PrintOptions = { ...a4Landscape, scope: "school", paper: "A3" };
const week = [7, 1, 2, 3, 4]; // Sunday to Thursday (ISO: 7 = Sunday)

describe("official printing (MF5)", () => {
  it("uses the owner's defaults: section A4 landscape, teacher A4 portrait, the school A3 landscape, in colour", () => {
    expect(defaultOptions("current", "section")).toEqual({ scope: "current", paper: "A4", orientation: "landscape", fit: true, color: true });
    expect(defaultOptions("current", "teacher")).toMatchObject({ paper: "A4", orientation: "portrait" });
    expect(defaultOptions("current", "master")).toMatchObject({ paper: "A3", orientation: "landscape" });
    expect(defaultOptions("sections", "teacher")).toMatchObject({ paper: "A4", orientation: "landscape" });
    expect(defaultOptions("teachers", "section")).toMatchObject({ paper: "A4", orientation: "portrait" });
    expect(defaultOptions("school", "section")).toMatchObject({ paper: "A3", orientation: "landscape" });
    expect(defaultOptions("stages", "section")).toMatchObject({ paper: "A3", orientation: "landscape", color: true });
  });

  it("takes the defaults from the stored preferences, and an own change wins only for that print", () => {
    const stored = {
      section: { paper: "a3", orientation: "portrait" }, teacher: { paper: "a4", orientation: "landscape" }, school: { paper: "a4", orientation: "landscape" }, printFit: false,
    };
    const defaults = printDefaultsFrom(stored);
    expect(defaultOptions("current", "section", defaults)).toEqual({ scope: "current", paper: "A3", orientation: "portrait", fit: false, color: true });
    expect(defaultOptions("teachers", "master", defaults)).toMatchObject({ paper: "A4", orientation: "landscape" });
    expect(defaultOptions("school", "section", defaults)).toMatchObject({ paper: "A4", orientation: "landscape" });
    expect(resolvePrint({ scope: "current", paper: "A4" }, "section", defaults)).toEqual({ scope: "current", paper: "A4", orientation: "portrait", fit: false, color: true });
    expect(resolvePrint({ scope: "current", color: false }, "section", defaults).color).toBe(false);
    expect(printDefaultsFrom(undefined).school).toEqual({ paper: "A3", orientation: "landscape" });
  });

  it("never prints a subject below 8 pt: the fit scale has a floor, and a wide grid is split instead", () => {
    expect(MIN_SUBJECT_PT).toBe(8);
    expect(MIN_TEACHER_PT).toBe(7);
    expect(MIN_SCALE * PRINT_BASE_PT).toBe(MIN_SUBJECT_PT);
    expect(printableWidth(a4Landscape)).toBe(277);
    expect(fitScale(a4Landscape, 8)).toBe(1); // 8 × 20 = 160 mm fits
    expect(fitScale(a4Landscape, 15)).toBeCloseTo(277 / 300);
    expect(fitScale(a4Landscape, 20)).toBe(MIN_SCALE); // 277 / 400 would be 6.9 pt: held at 8 pt
    expect(fitScale(a3Landscape, 36)).toBe(MIN_SCALE); // the whole school: 5.5 pt before, 8 pt now
    expect(fitScale({ ...a4Landscape, fit: false }, 36)).toBe(1);
  });

  it("keeps the whole school on one page when it fits at a readable size, else splits the days", () => {
    // 4 days × 4 lessons + the section column = 17 columns: fits A3 landscape (400 mm) at a scale of about 1.
    const small = planDays(a3Landscape, [7, 1, 2, 3], 4);
    expect(small.chunks).toEqual([[7, 1, 2, 3]]);
    expect(small.scale).toBeGreaterThanOrEqual(MIN_SCALE);
    // 5 days × 7 lessons = 36 columns: 25 columns fit at 8 pt, so 3 days per page.
    const school = planDays(a3Landscape, week, 7);
    expect(school.chunks).toEqual([[7, 1, 2], [3, 4]]);
    expect(school.scale).toBeGreaterThanOrEqual(MIN_SCALE);
    expect(school.scale).toBeLessThanOrEqual(1);
    // Every chunk really fits at the chosen scale.
    for (const chunk of school.chunks) expect((1 + chunk.length * 7) * 20 * school.scale).toBeLessThanOrEqual(printableWidth(a3Landscape) + 0.01);
    // A4 landscape holds fewer columns, so more pages; one day per page at worst.
    expect(planDays({ ...a3Landscape, paper: "A4" }, week, 7).chunks.length).toBeGreaterThan(2);
    expect(planDays({ ...a3Landscape, paper: "A4" }, week, 14).chunks.every((chunk) => chunk.length === 1)).toBe(true);
    // Without «ملاءمة الصفحة» the grid keeps its full size and only the split changes.
    expect(planDays({ ...a3Landscape, fit: false }, week, 7).scale).toBe(1);
  });

  it("writes the page size and an Arabic page number in the margin", () => {
    const css = pageCss({ ...a3Landscape });
    expect(css).toContain("size: A3 landscape");
    expect(css).toContain("counter(page, arabic-indic)");
    expect(css).toContain("counter(pages, arabic-indic)");
    expect(css).toContain("صفحة");
    expect(pageCss({ ...a3Landscape, paper: "A4", orientation: "portrait" }, false)).toContain("counter(page)");
  });
});
