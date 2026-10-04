import { readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { describe, expect, it } from "vitest";

// An undefined custom property makes a declaration silently invalid (no border, no colour, no gap), and neither
// Stylelint nor TypeScript notices. Every var(--x) used in the app's CSS must be declared in one of its files.
const stylesDir = resolve(process.cwd(), "src/styles");
const cssFiles = [resolve(process.cwd(), "src/styles.css"), ...readdirSync(stylesDir).filter((name) => name.endsWith(".css")).map((name) => join(stylesDir, name))];
const sources = cssFiles.map((file) => ({ file, css: readFileSync(file, "utf8") }));
// Provided by Tailwind at build time, not by our stylesheets.
const external = /^--tw-/;

describe("CSS custom properties", () => {
  it("are all declared before use", () => {
    const declared = new Set(sources.flatMap(({ css }) => [...css.matchAll(/(--[a-z0-9-]+)\s*:/gi)].map((match) => match[1])));
    const undefinedUses = sources.flatMap(({ file, css }) =>
      [...css.matchAll(/var\(\s*(--[a-z0-9-]+)/gi)]
        .map((match) => match[1])
        .filter((name) => !declared.has(name) && !external.test(name))
        .map((name) => `${file.replace(/^.*[\\/]src[\\/]/, "src/")}: ${name}`));
    expect([...new Set(undefinedUses)]).toEqual([]);
  });
});
