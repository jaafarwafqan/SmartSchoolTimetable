/**
 * Wraps a user-entered value (a name, a label such as "2026-2027") in Unicode bidi isolation marks
 * (FSI ... PDI) before it is embedded in an Arabic sentence, so digits and Latin text keep their own order
 * (DESIGN_SYSTEM.md 8). Works in plain strings, including aria-label and title attributes.
 */
export function isolate(value: string): string {
  return `⁨${value}⁩`;
}
