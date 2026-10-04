import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";
import { describe, expect, it } from "vitest";

// Spec 2.5 §2.4 / ADR 0024: no side drawers or sheets anywhere. ESLint (design-system/no-drawers) checks TSX
// identifiers; this test also covers file names and stylesheets.
const root = resolve(process.cwd(), "src");
const forbidden = /drawer|sheet/i;
const self = "styles/noDrawers.test.ts";

function files(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name);
    return statSync(path).isDirectory() ? files(path) : [path];
  });
}

describe("add patterns", () => {
  it("have no drawer or sheet files, components or CSS classes", () => {
    const offenders = files(root)
      .map((path) => relative(root, path).replaceAll("\\", "/"))
      .filter((path) => path !== self && /\.(tsx?|css)$/.test(path))
      .filter((path) => forbidden.test(path) || forbidden.test(readFileSync(join(root, path), "utf8").replace(/stylesheet/gi, "")));
    expect(offenders).toEqual([]);
  });
});
