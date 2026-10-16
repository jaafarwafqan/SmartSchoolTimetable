import { describe, expect, it } from "vitest";
import { blockSize, defaultOptions, fitScale, gridHeight, MIN_SCALE, MIN_SUBJECT_PT, MIN_TEACHER_PT, PRINT_BASE_PT, pageCss, planDays, planPacking, planSchoolByDays, printableWidth, printDefaultsFrom, resolvePrint, schoolByDaysSize, formatSize, type PrintOptions } from "./printing";

const a4Landscape: PrintOptions = { scope: "current", paper: "A4", orientation: "landscape", fit: true, color: true, perPage: "auto" };
const a3Landscape: PrintOptions = { ...a4Landscape, scope: "school", paper: "A3" };
const week = [7, 1, 2, 3, 4]; // Sunday to Thursday (ISO: 7 = Sunday)

describe("official printing (MF5)", () => {
  it("uses the owner's defaults: section A4 landscape, teacher A4 portrait, the school A3 landscape, in colour", () => {
    expect(defaultOptions("current", "section")).toEqual({ scope: "current", paper: "A4", orientation: "landscape", fit: true, color: true, perPage: "auto" });
    expect(defaultOptions("current", "teacher")).toMatchObject({ paper: "A4", orientation: "portrait" });
    expect(defaultOptions("current", "master")).toMatchObject({ paper: "A3", orientation: "auto" }); // the whole school chooses its orientation
    expect(defaultOptions("sections", "teacher")).toMatchObject({ paper: "A4", orientation: "landscape" });
    expect(defaultOptions("teachers", "section")).toMatchObject({ paper: "A4", orientation: "portrait" });
    expect(defaultOptions("school", "section")).toMatchObject({ paper: "A3", orientation: "auto" });
    expect(defaultOptions("stages", "section")).toMatchObject({ paper: "A3", orientation: "auto", color: true });
  });

  it("takes the defaults from the stored preferences, and an own change wins only for that print", () => {
    const stored = {
      section: { paper: "a3", orientation: "portrait" }, teacher: { paper: "a4", orientation: "landscape" }, school: { paper: "a4", orientation: "landscape" }, printFit: false,
    };
    const defaults = printDefaultsFrom(stored);
    expect(defaultOptions("current", "section", defaults)).toEqual({ scope: "current", paper: "A3", orientation: "portrait", fit: false, color: true, perPage: "auto" });
    expect(defaultOptions("teachers", "master", defaults)).toMatchObject({ paper: "A4", orientation: "landscape" });
    expect(defaultOptions("school", "section", defaults)).toMatchObject({ paper: "A4", orientation: "auto" });
    expect(resolvePrint({ scope: "current", paper: "A4" }, "section", defaults)).toEqual({ scope: "current", paper: "A4", orientation: "portrait", fit: false, color: true, perPage: "auto" });
    expect(resolvePrint({ scope: "teachers", perPage: 4 }, "section", defaults).perPage).toBe(4);
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

  it("writes the page size with no margin, so the browser has no room for its own date, title and address", () => {
    expect(pageCss({ paper: "A3", orientation: "landscape" })).toBe("@page { size: A3 landscape; margin: 0; }");
    expect(pageCss({ paper: "A4", orientation: "portrait" })).toContain("size: A4 portrait");
    expect(formatSize({ paper: "A3", orientation: "portrait" })).toEqual([297, 420]);
  });
});

describe("the whole school by days (owner, 1.0.0 testing)", () => {
  const a3: PrintOptions = { scope: "school", paper: "A3", orientation: "auto", fit: true, color: true, perPage: "auto" };

  it("puts a 12-section school of 30 lessons a week (5 days × 6) on ONE A3 page, choosing portrait automatically, at 8 pt or more", () => {
    const plan = planSchoolByDays(a3, [4, 4, 4], week, 6);
    expect(plan.pages).toEqual([{ stages: [0, 1, 2], days: week }]);
    expect(plan.perStage).toBe(false);
    expect(plan.format).toEqual({ paper: "A3", orientation: "portrait" });
    expect(plan.scale).toBeGreaterThanOrEqual(MIN_SCALE);
    // It really fits at that scale, in both directions.
    const [width, height] = schoolByDaysSize(12, 5, 6);
    expect(width * plan.scale).toBeLessThanOrEqual(formatSize(plan.format)[0] - 20 + 0.01);
    expect(height * plan.scale).toBeLessThanOrEqual(gridHeight(plan.format) + 0.01);
    // A chosen orientation is kept: landscape cannot hold 30 rows at 8 pt, so the days are split over pages (all stages on each).
    const landscape = planSchoolByDays({ ...a3, orientation: "landscape" }, [4, 4, 4], week, 6);
    expect(landscape.format.orientation).toBe("landscape");
    expect(landscape.pages.length).toBeGreaterThan(1);
    expect(landscape.pages.every((page) => page.stages.length === 3)).toBe(true);
  });

  it("splits by stage only when the school truly cannot fit, and splits a stage by days only when it alone is too tall", () => {
    // 7 lessons a day: 35 rows of three lines need 457 mm, more than A3 holds at 8 pt. Splitting by stage would not help (the rows stay),
    // so the whole school stays on each page and the days are split: two pages, not six.
    const seven = planSchoolByDays(a3, [2, 2, 2], week, 7);
    expect(seven.perStage).toBe(false);
    expect(seven.pages).toEqual([{ stages: [0, 1, 2], days: [7, 1, 2] }, { stages: [0, 1, 2], days: [3, 4] }]);
    expect(seven.scale).toBeGreaterThanOrEqual(MIN_SCALE);
    // 24 sections are too wide for one page: a page per stage, every stage with all its days when they fit.
    const big = planSchoolByDays(a3, [8, 8, 8], week, 6);
    expect(big.perStage).toBe(true);
    expect(new Set(big.pages.map((page) => page.stages[0]))).toEqual(new Set([0, 1, 2]));
    expect(big.scale).toBeGreaterThanOrEqual(MIN_SCALE);
    expect(big.pages.every((page) => page.days.length === 5)).toBe(true);
    // On A4 a stage of 8 sections with 7 lessons a day no longer fits in height: its days are split, every stage the same way.
    const small = planSchoolByDays({ ...a3, paper: "A4" }, [8, 8], week, 7);
    expect(small.pages.length).toBeGreaterThan(2);
    expect(small.pages.flatMap((page) => page.days).length).toBe(10);
    // «صفحة لكل مرحلة» is always a page per stage.
    expect(planSchoolByDays({ ...a3, scope: "stages" }, [4, 4, 4], week, 6).pages).toHaveLength(3);
  });
});

describe("teachers and sections packed on a page (owner, 1.0.0 testing)", () => {
  const teachersA4: PrintOptions = { scope: "teachers", paper: "A4", orientation: "portrait", fit: true, color: true, perPage: "auto" };
  const block = blockSize(8, 5); // 7 lessons + the day column, 5 days

  it("«تلقائي» packs as many as fit at a readable size; a block is never split and every one fits", () => {
    const plan = planPacking(teachersA4, block, 22);
    expect(plan.perPage).toBeGreaterThanOrEqual(2);
    expect(plan.scale).toBeGreaterThanOrEqual(MIN_SCALE);
    const width = plan.columns * block[0] + (plan.columns - 1) * 5;
    const height = plan.rows * block[1] + (plan.rows - 1) * 5;
    expect(width * plan.scale).toBeLessThanOrEqual(printableWidth(teachersA4) + 0.01);
    expect(height * plan.scale).toBeLessThanOrEqual(gridHeight(plan.format) + 0.01);
    expect(plan.columns * plan.rows).toBeGreaterThanOrEqual(plan.perPage);
    // A3 holds more than A4.
    expect(planPacking({ ...teachersA4, paper: "A3" }, block, 22).perPage).toBeGreaterThan(plan.perPage);
    // Never more than there are teachers.
    expect(planPacking(teachersA4, block, 1).perPage).toBe(1);
  });

  it("keeps a fixed number when it is readable, and lowers it (saying so) when it is not", () => {
    expect(planPacking({ ...teachersA4, perPage: 1 }, block, 22)).toMatchObject({ perPage: 1, reduced: false, scale: 1 });
    expect(planPacking({ ...teachersA4, perPage: 2 }, block, 22)).toMatchObject({ perPage: 2, reduced: false });
    const six = planPacking({ ...teachersA4, perPage: 6 }, block, 22);
    expect(six.perPage).toBeLessThanOrEqual(6);
    expect(six.scale).toBeGreaterThanOrEqual(MIN_SCALE);
    expect(six.reduced).toBe(six.perPage < 6);
    expect(planPacking({ ...teachersA4, paper: "A3", orientation: "landscape", perPage: 6 }, block, 22)).toMatchObject({ perPage: 6, reduced: false });
  });
});
