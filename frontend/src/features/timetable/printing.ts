import { messages } from "../../i18n/messages";

/** MF5: what a print job covers, on which paper, and whether the grid shrinks to fit. */
export type PrintScope = "current" | "sections" | "teachers" | "school";
export type PaperSize = "A4" | "A3";
export type Orientation = "portrait" | "landscape";
export type PrintOptions = { scope: PrintScope; paper: PaperSize; orientation: Orientation; fit: boolean };
export type GridView = "section" | "teacher" | "master";

type Sizing = { paper: PaperSize; orientation: Orientation };

/** Default paper and orientation per kind of print job (stored in the owner's preferences, M2). */
export type PrintDefaults = { section: Sizing; teacher: Sizing; school: Sizing; fit: boolean };

/**
 * The built-in defaults: a section on A4 landscape, a teacher on A4 portrait, the whole school on A3 landscape
 * (split over pages with the column headers repeated when it is longer than one page).
 */
export const builtInPrintDefaults: PrintDefaults = {
  section: { paper: "A4", orientation: "landscape" },
  teacher: { paper: "A4", orientation: "portrait" },
  school: { paper: "A3", orientation: "landscape" },
  fit: true,
};

type StoredSizing = { paper: string; orientation: string };

/** The stored preferences (lower-case values from the API) as print defaults; the built-in ones until they load. */
export function printDefaultsFrom(stored: { section: StoredSizing; teacher: StoredSizing; school: StoredSizing; printFit: boolean } | undefined): PrintDefaults {
  if (!stored) return builtInPrintDefaults;
  const sizing = (value: StoredSizing): Sizing => ({
    paper: value.paper === "a3" ? "A3" : "A4",
    orientation: value.orientation === "portrait" ? "portrait" : "landscape",
  });
  return { section: sizing(stored.section), teacher: sizing(stored.teacher), school: sizing(stored.school), fit: stored.printFit };
}

export function defaultOptions(scope: PrintScope, view: GridView, defaults: PrintDefaults = builtInPrintDefaults): PrintOptions {
  const kind: GridView = scope === "sections" ? "section" : scope === "teachers" ? "teacher" : scope === "school" ? "master" : view;
  const sizing = kind === "master" ? defaults.school : kind === "teacher" ? defaults.teacher : defaults.section;
  return { scope, paper: sizing.paper, orientation: sizing.orientation, fit: defaults.fit };
}

/** What the owner changed for THIS print; everything left out follows the defaults of the chosen job. */
export type PrintChoice = { scope: PrintScope; paper?: PaperSize; orientation?: Orientation; fit?: boolean };

export function resolvePrint(choice: PrintChoice, view: GridView, defaults: PrintDefaults): PrintOptions {
  const base = defaultOptions(choice.scope, view, defaults);
  return { scope: choice.scope, paper: choice.paper ?? base.paper, orientation: choice.orientation ?? base.orientation, fit: choice.fit ?? base.fit };
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
