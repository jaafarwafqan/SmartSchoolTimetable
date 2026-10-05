const LRI = "⁦";
const FSI = "⁨";
const PDI = "⁩";

/**
 * Numeric ranges, times and dates ("2026-2027", "2026 - 2027", "08:30", "2026/09/01") and Latin runs
 * (codes, file names, usernames). Inside right-to-left text the Unicode bidi algorithm reverses a range
 * such as "2026 - 2027"; each run is wrapped in a left-to-right isolate so it keeps its logical order.
 */
const ltrRun = /[0-9٠-٩]+(?:\s*[-–/:.]\s*[0-9٠-٩]+)+|[A-Za-z][A-Za-z0-9._@-]*/g;

/** Returns the text with every numeric range and Latin run wrapped in LRI…PDI (for strings, options, aria). */
export function ltrRuns(value: string): string {
  return value.replace(ltrRun, (run) => `${LRI}${run}${PDI}`);
}

/**
 * Wraps a user-entered value (a name, a label such as "2026-2027") in a first-strong isolate before it is
 * embedded in an Arabic sentence, and isolates its own numeric ranges (DESIGN_SYSTEM.md 8). Works in plain
 * strings, including aria-label and title attributes.
 */
export function isolate(value: string): string {
  return `${FSI}${ltrRuns(value)}${PDI}`;
}
