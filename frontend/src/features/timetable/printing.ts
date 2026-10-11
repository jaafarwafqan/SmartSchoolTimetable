import { messages } from "../../i18n/messages";

/** MF5: what a print job covers, on which paper, and whether the grid shrinks to fit. */
export type PrintScope = "current" | "sections" | "teachers" | "school" | "stages";
export type PaperSize = "A4" | "A3";
export type Orientation = "portrait" | "landscape";
/** `color` false is the black-and-white mode: no fills, bold subject names, borders and stage headers carry the structure. */
export type PrintOptions = { scope: PrintScope; paper: PaperSize; orientation: Orientation; fit: boolean; color: boolean };
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
  const kind: GridView = scope === "sections" ? "section" : scope === "teachers" ? "teacher" : scope === "school" || scope === "stages" ? "master" : view;
  const sizing = kind === "master" ? defaults.school : kind === "teacher" ? defaults.teacher : defaults.section;
  return { scope, paper: sizing.paper, orientation: sizing.orientation, fit: defaults.fit, color: true };
}

/** What the owner changed for THIS print; everything left out follows the defaults of the chosen job. */
export type PrintChoice = { scope: PrintScope; paper?: PaperSize; orientation?: Orientation; fit?: boolean; color?: boolean };

export function resolvePrint(choice: PrintChoice, view: GridView, defaults: PrintDefaults): PrintOptions {
  const base = defaultOptions(choice.scope, view, defaults);
  return { scope: choice.scope, paper: choice.paper ?? base.paper, orientation: choice.orientation ?? base.orientation, fit: choice.fit ?? base.fit, color: choice.color ?? base.color };
}

/** Printable width in millimetres (10 mm side margins). */
export function printableWidth(options: PrintOptions): number {
  const [short, long] = options.paper === "A3" ? [297, 420] : [210, 297];
  return (options.orientation === "landscape" ? long : short) - 20;
}

/** The print base size and the smallest sizes that stay readable on paper: 8 pt for subjects, 7 pt for teacher names. */
export const PRINT_BASE_PT = 10;
export const MIN_SUBJECT_PT = 8;
export const MIN_TEACHER_PT = 7;
export const MIN_SCALE = MIN_SUBJECT_PT / PRINT_BASE_PT;

/** A grid column needs about 20 mm at full size (10 pt; the longest two-word subject wraps onto two lines). */
const COLUMN_MM = 20;

/**
 * «ملاءمة الصفحة»: a font scale so every column fits the page width. It never goes below {@link MIN_SCALE} (a subject name
 * stays at 8 pt or more): what does not fit at that size is split over pages by {@link planDays} instead of shrinking further.
 * Without fit the grid keeps its full size and only wraps.
 */
export function fitScale(options: PrintOptions, columns: number): number {
  if (!options.fit) return 1;
  const needed = Math.max(1, columns) * COLUMN_MM;
  return Math.max(MIN_SCALE, Math.min(1, printableWidth(options) / needed));
}

/** How many columns (the section column included) fit the printable width at a scale. */
function columnsThatFit(options: PrintOptions, scale: number): number {
  return Math.floor(printableWidth(options) / (COLUMN_MM * scale) + 1e-9);
}

/** The days split into chunks of at most `perChunk` days. */
function chunked(days: number[], perChunk: number): number[][] {
  const chunks: number[][] = [];
  for (let index = 0; index < days.length; index += perChunk)
    chunks.push(days.slice(index, index + perChunk));
  return chunks;
}

/**
 * The whole-school layout: the font scale and which days go on which page. All the days on one page when they fit at a readable
 * size; otherwise the fewest pages at the smallest readable size, then the largest scale (down from 1) that keeps that page count.
 * Every page repeats the column headers, the official header and the signature footer.
 */
export function planDays(options: PrintOptions, days: number[], lessonsPerDay: number): { scale: number; chunks: number[][] } {
  const lessons = Math.max(1, lessonsPerDay);
  const total = 1 + days.length * lessons;
  const floor = options.fit ? MIN_SCALE : 1;
  const wholeScale = options.fit ? fitScale(options, total) : 1;
  if (columnsThatFit(options, wholeScale) >= total || days.length <= 1)
    return { scale: wholeScale, chunks: [days] };
  const perChunkAt = (scale: number) => Math.max(1, Math.floor((columnsThatFit(options, scale) - 1) / lessons));
  const pages = chunked(days, perChunkAt(floor)).length;
  let scale = floor;
  for (let candidate = 1; candidate > floor + 1e-9; candidate = Math.round((candidate - 0.05) * 100) / 100) {
    if (chunked(days, perChunkAt(candidate)).length === pages) { scale = candidate; break; }
  }
  return { scale, chunks: chunked(days, perChunkAt(scale)) };
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
