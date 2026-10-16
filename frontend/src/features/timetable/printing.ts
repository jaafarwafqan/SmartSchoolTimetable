/** MF5: what a print job covers, on which paper, and whether the grid shrinks to fit. */
export type PrintScope = "current" | "sections" | "teachers" | "school" | "stages";
export type PaperSize = "A4" | "A3";
export type Orientation = "portrait" | "landscape";
/** «تلقائي»: the whole-school print takes the orientation that fits best (the larger readable size on one page). */
export type OrientationChoice = Orientation | "auto";
/** Teachers or sections per printed page in a batch: «تلقائي» packs as many as fit at a readable size. */
export type PerPage = "auto" | 1 | 2 | 4 | 6;
export const perPageChoices: readonly PerPage[] = ["auto", 1, 2, 4, 6];
/** `color` false is the black-and-white mode: no fills, bold subject names, borders and stage headers carry the structure. */
export type PrintOptions = { scope: PrintScope; paper: PaperSize; orientation: OrientationChoice; fit: boolean; color: boolean; perPage: PerPage };
/** The concrete paper of one printed page. */
export type PageFormat = { paper: PaperSize; orientation: Orientation };
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
  // The whole school chooses its orientation itself (owner, 1.0.0 testing): the one that fits it on one page at the larger size.
  return { scope, paper: sizing.paper, orientation: kind === "master" ? "auto" : sizing.orientation, fit: defaults.fit, color: true, perPage: "auto" };
}

/** What the owner changed for THIS print; everything left out follows the defaults of the chosen job. */
export type PrintChoice = { scope: PrintScope; paper?: PaperSize; orientation?: OrientationChoice; fit?: boolean; color?: boolean; perPage?: PerPage };

export function resolvePrint(choice: PrintChoice, view: GridView, defaults: PrintDefaults): PrintOptions {
  const base = defaultOptions(choice.scope, view, defaults);
  return {
    scope: choice.scope, paper: choice.paper ?? base.paper, orientation: choice.orientation ?? base.orientation, fit: choice.fit ?? base.fit,
    color: choice.color ?? base.color, perPage: choice.perPage ?? base.perPage,
  };
}

/**
 * The page geometry at full size (10 pt). The page has no browser margin at all (`@page { margin: 0 }`), so Chromium has nowhere to draw
 * its own date, title and address; the document pads every format itself and prints «صفحة ١ من ٣» in its own footer.
 */
export const PAGE_PADDING_MM = 10;
/** The school logo, name, title, year and the entity: four lines beside an 18 mm logo, with its rule and 3 mm space below (measured: 30 + 3 mm). */
export const PAGE_HEADER_MM = 34;
/** The signature footer and the page number, which wrap onto two lines on a narrow page (measured: 22 mm on A4 portrait). */
export const PAGE_FOOTER_MM = 23;
/**
 * A lesson row of a printed grid is exactly three lines: the subject on at most two, the teacher or section on one (the print stylesheet
 * clamps them), line height 1.1 and 0.15 em padding: 3.6 em = 12.7 mm at 10 pt, 13 mm with the borders (measured). Fixed heights make the page plan exact for any school data.
 */
export const ROW_MM = 13;
/** A header row of the school by days (a stage, or a section label: one line). */
export const HEADER_ROW_MM = 6;
/** The day and the lesson-number columns of the whole school by days (the number column also holds the 12-hour time). */
export const DAY_COLUMN_MM = 15;
export const NUMBER_COLUMN_MM = 18;
/** The title line of a packed teacher or section block (name and weekly load). */
export const BLOCK_TITLE_MM = 8;
/** A day row of a teacher or section grid: four clamped lines (the subject on two, the section or teacher on two): 4.7 em + borders. */
export const BLOCK_ROW_MM = 17;
/** A header row of a teacher or section grid: «الحصة ١» and its time, or a session's clock row, on at most two lines (time never below 7 pt). */
export const BLOCK_HEADER_ROW_MM = 12.5;
/** The space between packed blocks. */
export const BLOCK_GAP_MM = 5;

/** Paper size in millimetres: [width, height] of the format as it is printed. */
export function formatSize(format: PageFormat): [number, number] {
  const [short, long] = format.paper === "A3" ? [297, 420] : [210, 297];
  return format.orientation === "landscape" ? [long, short] : [short, long];
}

/** The format of options whose orientation is chosen (an «تلقائي» orientation counts as landscape until a planner decides). */
export function formatOf(options: { paper: PaperSize; orientation: OrientationChoice }): PageFormat {
  return { paper: options.paper, orientation: options.orientation === "portrait" ? "portrait" : "landscape" };
}

/** Printable width in millimetres (10 mm side padding). */
export function printableWidth(options: { paper: PaperSize; orientation: OrientationChoice }): number {
  return formatSize(formatOf(options))[0] - 2 * PAGE_PADDING_MM;
}

/** Printable height for the grid: the format less its padding, the official header and the signature footer. */
export function gridHeight(format: PageFormat): number {
  return formatSize(format)[1] - 2 * PAGE_PADDING_MM - PAGE_HEADER_MM - PAGE_FOOTER_MM;
}

/** The print base size and the smallest sizes that stay readable on paper: 8 pt for subjects, 7 pt for teacher names. */
export const PRINT_BASE_PT = 10;
export const MIN_SUBJECT_PT = 8;
export const MIN_TEACHER_PT = 7;
export const MIN_SCALE = MIN_SUBJECT_PT / PRINT_BASE_PT;

/** A grid column needs about 20 mm at full size (10 pt; the longest two-word subject wraps onto two lines). */
export const COLUMN_MM = 20;

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

/** The largest scale (at most 1) at which a box of `width` × `height` mm (at full size) fits `space`. */
function scaleFor(space: [number, number], width: number, height: number): number {
  return Math.min(1, space[0] / Math.max(1, width), space[1] / Math.max(1, height));
}

/** The whole school by days (rows: days × lessons; columns: sections grouped by stage): its size in mm at full size. */
export function schoolByDaysSize(sectionCount: number, dayCount: number, lessonsPerDay: number): [number, number] {
  return [DAY_COLUMN_MM + NUMBER_COLUMN_MM + Math.max(1, sectionCount) * COLUMN_MM, 2 * HEADER_ROW_MM + Math.max(1, dayCount) * Math.max(1, lessonsPerDay) * ROW_MM];
}

/** One printed page of the whole school by days: the stages on it (their indices) and the days. */
export type SchoolPage = { stages: number[]; days: number[] };
export type SchoolPlan = { format: PageFormat; scale: number; pages: SchoolPage[]; perStage: boolean };

/**
 * The whole school by days (owner, 1.0.0 testing): ONE page when it fits at a readable size (subjects 8 pt or more), in the orientation that
 * gives the larger size when the orientation is «تلقائي». When it truly cannot fit, the fewest pages of two ways: every stage on each page with
 * the days split (when the school is too tall: rows are days × lessons, whatever the sections), or a page per stage (when it is too wide),
 * its days split too if one stage alone is still too tall. Headers repeat on every page. «صفحة لكل مرحلة» always prints a page per stage.
 */
export function planSchoolByDays(options: PrintOptions, stageSections: number[], days: number[], lessonsPerDay: number): SchoolPlan {
  const formats: PageFormat[] = options.orientation === "auto"
    ? [{ paper: options.paper, orientation: "portrait" }, { paper: options.paper, orientation: "landscape" }]
    : [{ paper: options.paper, orientation: options.orientation }];
  const floor = options.fit ? MIN_SCALE : 1;
  const all = stageSections.map((_, index) => index);
  const total = stageSections.reduce((sum, count) => sum + count, 0);
  const widthScale = (format: PageFormat, sections: number) => Math.min(1, (formatSize(format)[0] - 2 * PAGE_PADDING_MM) / schoolByDaysSize(sections, 1, lessonsPerDay)[0]);
  const heightScale = (format: PageFormat, dayCount: number) => Math.min(1, gridHeight(format) / schoolByDaysSize(1, dayCount, lessonsPerDay)[1]);
  const usable = (scale: number) => (options.fit ? scale : scale >= 1 - 1e-9 ? 1 : 0);
  const daysPerPage = (format: PageFormat, scale: number) =>
    Math.max(1, Math.floor((gridHeight(format) / scale - 2 * HEADER_ROW_MM) / (ROW_MM * Math.max(1, lessonsPerDay)) + 1e-9));
  if (options.scope !== "stages") {
    const whole = formats.map((format) => ({ format, scale: usable(Math.min(widthScale(format, total), heightScale(format, days.length))) }))
      .reduce((first, second) => (second.scale > first.scale + 1e-9 ? second : first));
    if (whole.scale >= floor - 1e-9) return { format: whole.format, scale: whole.scale, pages: [{ stages: all, days }], perStage: false };
  }
  type Candidate = SchoolPlan & { order: number };
  const candidates: Candidate[] = [];
  formats.forEach((format, index) => {
    // Every stage on each page, the days split: possible when the whole school is narrow enough at a readable size.
    const wide = usable(widthScale(format, total));
    if (options.scope !== "stages" && wide >= floor - 1e-9) {
      const parts = chunked(days, daysPerPage(format, wide));
      candidates.push({ format, scale: wide, pages: parts.map((part) => ({ stages: all, days: part })), perStage: false, order: index });
    }
    // A page per stage at the size the widest stage allows (all days when they fit, else split).
    const width = usable(widthScale(format, Math.max(1, ...stageSections)));
    const scale = Math.max(floor, Math.min(width || floor, Math.max(heightScale(format, days.length), floor)));
    const parts = chunked(days, daysPerPage(format, scale));
    candidates.push({ format, scale, pages: all.flatMap((stage) => parts.map((part) => ({ stages: [stage], days: part }))), perStage: true, order: 10 + index });
  });
  // The fewest pages; then the larger size; then the whole school on each page before a page per stage.
  const chosen = candidates.reduce((first, second) =>
    second.pages.length < first.pages.length
    || (second.pages.length === first.pages.length && (second.scale > first.scale + 1e-9 || (Math.abs(second.scale - first.scale) <= 1e-9 && second.order < first.order)))
      ? second : first);
  return { format: chosen.format, scale: chosen.scale, pages: chosen.pages, perStage: chosen.perStage };
}

/** Packed teacher or section blocks: how many go on a page, in how many columns and rows, at which scale. */
export type PackPlan = { format: PageFormat; perPage: number; columns: number; rows: number; scale: number; reduced: boolean };

/** The size in mm (full size) of one teacher or section block: its title, the lesson-number header rows and a row per day. */
export function blockSize(gridColumns: number, dayCount: number, headerRows = 1): [number, number] {
  return [Math.max(1, gridColumns) * COLUMN_MM, BLOCK_TITLE_MM + headerRows * BLOCK_HEADER_ROW_MM + Math.max(1, dayCount) * BLOCK_ROW_MM];
}

/**
 * «عدد المعلمين في الصفحة» (owner, 1.0.0 testing): several teachers (or sections) on one page, never one split over two. «تلقائي» takes the
 * most blocks that fit at a readable size, then the largest size for that number. A fixed number that cannot be read at 8 pt is lowered to
 * the most that can (`reduced`).
 */
export function planPacking(options: PrintOptions, block: [number, number], total: number): PackPlan {
  const format = formatOf(options);
  const space: [number, number] = [formatSize(format)[0] - 2 * PAGE_PADDING_MM, gridHeight(format)];
  const floor = options.fit ? MIN_SCALE : 1;
  const layoutFor = (count: number) => {
    let found = { columns: 1, rows: count, scale: 0 };
    for (let columns = 1; columns <= count; columns++) {
      const rows = Math.ceil(count / columns);
      const scale = scaleFor(space, columns * block[0] + (columns - 1) * BLOCK_GAP_MM, rows * block[1] + (rows - 1) * BLOCK_GAP_MM);
      if (scale > found.scale + 1e-9) found = { columns, rows, scale };
    }
    return { ...found, scale: options.fit ? found.scale : found.scale >= 1 - 1e-9 ? 1 : 0 };
  };
  const most = Math.max(1, total);
  const fits = (count: number) => layoutFor(count).scale >= floor - 1e-9;
  const wanted = options.perPage === "auto" ? most : Math.min(options.perPage, most);
  let count = 1;
  if (options.perPage === "auto") {
    for (let candidate = Math.min(most, 12); candidate >= 1; candidate--) if (fits(candidate)) { count = candidate; break; }
  } else {
    count = wanted;
    while (count > 1 && !fits(count)) count--;
  }
  const layout = layoutFor(count);
  return { format, perPage: count, columns: layout.columns, rows: layout.rows, scale: Math.max(floor, Math.min(1, layout.scale)), reduced: options.perPage !== "auto" && count < wanted };
}

/**
 * The @page rule of the job: paper and orientation with NO margin, so the browser has no room for its own header or footer (date, title,
 * address); the document pads each format and numbers its pages itself.
 */
export function pageCss(format: PageFormat): string {
  return `@page { size: ${format.paper} ${format.orientation}; margin: 0; }`;
}
