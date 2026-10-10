import { messages } from "../../i18n/messages";

/** MF5: what a print job covers, on which paper, and whether the grid shrinks to fit. */
export type PrintScope = "current" | "sections" | "teachers" | "school";
export type PaperSize = "A4" | "A3";
export type Orientation = "portrait" | "landscape";
export type PrintOptions = { scope: PrintScope; paper: PaperSize; orientation: Orientation; fit: boolean };
export type GridView = "section" | "teacher" | "master";

/**
 * The owner's defaults: a section on A4 landscape, a teacher on A4 portrait, the whole school on A3 landscape
 * (split over pages with the column headers repeated when it is longer than one page).
 */
export function defaultOptions(scope: PrintScope, view: GridView): PrintOptions {
  const kind: GridView = scope === "sections" ? "section" : scope === "teachers" ? "teacher" : scope === "school" ? "master" : view;
  if (kind === "master") return { scope, paper: "A3", orientation: "landscape", fit: true };
  if (kind === "teacher") return { scope, paper: "A4", orientation: "portrait", fit: true };
  return { scope, paper: "A4", orientation: "landscape", fit: true };
}

/** Printable width in millimetres (10 mm side margins). */
export function printableWidth(options: PrintOptions): number {
  const [short, long] = options.paper === "A3" ? [297, 420] : [210, 297];
  return (options.orientation === "landscape" ? long : short) - 20;
}

/**
 * «ملاءمة الصفحة»: a font scale so every column fits the page width (never cut). A grid column needs about 20 mm at full
 * size (10 pt; the longest two-word subject wraps onto two lines); the scale never goes below 0.45 (about 4.5 pt), and
 * without fit the grid keeps its full size and only wraps.
 */
export function fitScale(options: PrintOptions, columns: number): number {
  if (!options.fit) return 1;
  const needed = Math.max(1, columns) * 20;
  return Math.max(0.45, Math.min(1, printableWidth(options) / needed));
}

/**
 * The @page rule of the job: paper, orientation, margins and «صفحة ١ من ٣» in the bottom margin (Chromium margin boxes),
 * with the school's numerals.
 */
export function pageCss(options: PrintOptions, arabicIndic = true): string {
  const text = messages.school.printing;
  const quote = (value: string) => `"${value.replaceAll('"', "")}"`;
  const digits = arabicIndic ? ", arabic-indic" : "";
  return `@page { size: ${options.paper} ${options.orientation}; margin: 10mm 10mm 14mm 10mm; `
    + `@bottom-center { content: ${quote(text.pageBefore)} counter(page${digits}) ${quote(text.pageMiddle)} counter(pages${digits}); `
    + `color: black; font-size: 9pt; } }`;
}
