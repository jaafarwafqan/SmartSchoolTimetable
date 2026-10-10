import { describe, expect, it } from "vitest";
import { messages } from "./messages";

// Spec 2.5 §2.3: every fixed UI text is Arabic. Walks the whole dictionary (template functions are called with
// Arabic sample values) and fails on any Latin letter. Values that are data, not prose, are listed explicitly.
const allowedValues = new Set(["/", ":", "رمز-الاسترداد.txt"]);
// File format codes the owner must recognise: the upload hint and «حفظ بتنسيق PDF» in the print dialog (codes are allowed by the spec).
const allowedTerms = /\b(?:PNG|JPEG|WebP|PDF)\b/g;
const sample = "قيمة";

function visibleTexts(value: unknown, path: string): { path: string; text: string }[] {
  if (typeof value === "string") return [{ path, text: value }];
  if (typeof value === "function") {
    const result: unknown = value(...Array.from({ length: value.length }, () => sample));
    return typeof result === "string" ? [{ path, text: result }] : [];
  }
  if (Array.isArray(value)) return value.flatMap((item, index) => visibleTexts(item, `${path}[${index}]`));
  if (value && typeof value === "object") {
    return Object.entries(value).flatMap(([key, item]) => visibleTexts(item, path ? `${path}.${key}` : key));
  }
  return [];
}

describe("Arabic dictionary", () => {
  it("contains no Latin letters in visible text", () => {
    const latin = visibleTexts(messages, "")
      .filter(({ text }) => !allowedValues.has(text) && /[A-Za-z]/.test(text.replace(allowedTerms, "")))
      .map(({ path, text }) => `${path}: ${text}`);
    expect(latin).toEqual([]);
  });
});
