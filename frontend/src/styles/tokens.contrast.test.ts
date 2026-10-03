/// <reference types="node" />
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

// DESIGN_SYSTEM.md section 12.3: every documented text/background pair must meet WCAG AA.
// Vitest runs from the frontend folder; CSS imports are stubbed in tests, so the file is read from disk.
const tokensCss = readFileSync(resolve(process.cwd(), "src/styles/tokens.css"), "utf8");

function tokenColors(): Map<string, string> {
  const colors = new Map<string, string>();
  for (const match of tokensCss.matchAll(/--color-([a-z0-9-]+):\s*(#[0-9a-f]{6})\b/gi)) {
    colors.set(match[1], match[2].toLowerCase());
  }
  return colors;
}

function channel(value: number): number {
  const srgb = value / 255;
  return srgb <= 0.04045 ? srgb / 12.92 : ((srgb + 0.055) / 1.055) ** 2.4;
}

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((offset) => channel(Number.parseInt(hex.slice(offset, offset + 2), 16)));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

export function contrastRatio(foreground: string, background: string): number {
  const [light, dark] = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
  return (light + 0.05) / (dark + 0.05);
}

const colors = tokenColors();

function color(name: string): string {
  const value = colors.get(name);
  if (!value) throw new Error(`Token --color-${name} is missing from tokens.css`);
  return value;
}

const subjectTokens = Array.from({ length: 10 }, (_, index) => `subject-${index + 1}`);

const textPairs: Array<[string, string]> = [
  ...["ink", "ink-muted", "ink-subtle"].flatMap((text) =>
    ["surface", "canvas"].map((background): [string, string] => [text, background])),
  ["on-primary", "primary"],
  ["on-primary", "primary-hover"],
  ["primary-ink", "primary-soft"],
  ["danger", "danger-soft"],
  ["success", "success-soft"],
  ["warning", "warning-soft"],
  ...subjectTokens.map((subject): [string, string] => ["on-subject", subject]),
];

describe("design tokens contrast (WCAG 2.1 AA)", () => {
  it("parses every colour token from tokens.css", () => {
    expect(colors.size).toBeGreaterThanOrEqual(32);
  });

  it.each(textPairs)("text %s on %s reaches 4.5:1", (text, background) => {
    expect(contrastRatio(color(text), color(background))).toBeGreaterThanOrEqual(4.5);
  });

  it("control border line-strong on surface reaches 3:1", () => {
    expect(contrastRatio(color("line-strong"), color("surface"))).toBeGreaterThanOrEqual(3);
  });

  it("computes known reference ratios correctly", () => {
    expect(contrastRatio("#000000", "#ffffff")).toBeCloseTo(21, 5);
    expect(contrastRatio("#ffffff", "#ffffff")).toBeCloseTo(1, 5);
  });
});
