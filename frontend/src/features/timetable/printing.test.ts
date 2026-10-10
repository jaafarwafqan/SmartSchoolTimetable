import { describe, expect, it } from "vitest";
import { defaultOptions, fitScale, pageCss, printableWidth } from "./printing";

describe("official printing (MF5)", () => {
  it("uses the owner's defaults: section A4 landscape, teacher A4 portrait, the school A3 landscape", () => {
    expect(defaultOptions("current", "section")).toEqual({ scope: "current", paper: "A4", orientation: "landscape", fit: true });
    expect(defaultOptions("current", "teacher")).toMatchObject({ paper: "A4", orientation: "portrait" });
    expect(defaultOptions("current", "master")).toMatchObject({ paper: "A3", orientation: "landscape" });
    expect(defaultOptions("sections", "teacher")).toMatchObject({ paper: "A4", orientation: "landscape" });
    expect(defaultOptions("teachers", "section")).toMatchObject({ paper: "A4", orientation: "portrait" });
    expect(defaultOptions("school", "section")).toMatchObject({ paper: "A3", orientation: "landscape" });
  });

  it("shrinks a wide grid to the page width only when «ملاءمة الصفحة» is on", () => {
    const a4Landscape = { scope: "current" as const, paper: "A4" as const, orientation: "landscape" as const, fit: true };
    expect(printableWidth(a4Landscape)).toBe(277);
    expect(fitScale(a4Landscape, 8)).toBe(1); // 8 × 20 = 160 mm fits
    expect(fitScale(a4Landscape, 20)).toBeCloseTo(277 / 400);
    expect(fitScale({ ...a4Landscape, paper: "A3" }, 36)).toBeCloseTo(400 / 720); // the whole school: about 5.5 pt
    expect(fitScale({ ...a4Landscape, paper: "A3" }, 200)).toBe(0.45);
    expect(fitScale({ ...a4Landscape, fit: false }, 36)).toBe(1);
  });

  it("writes the page size and an Arabic page number in the margin", () => {
    const css = pageCss({ scope: "school", paper: "A3", orientation: "landscape", fit: true });
    expect(css).toContain("size: A3 landscape");
    expect(css).toContain("counter(page, arabic-indic)");
    expect(css).toContain("counter(pages, arabic-indic)");
    expect(css).toContain("صفحة");
    expect(pageCss({ scope: "school", paper: "A4", orientation: "portrait", fit: true }, false)).toContain("counter(page)");
  });
});
