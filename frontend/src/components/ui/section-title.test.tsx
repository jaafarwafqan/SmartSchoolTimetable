import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { render, screen } from "@testing-library/react";
import { Sun } from "lucide-react";
import { describe, expect, it } from "vitest";
import { SectionTitle } from "./section-title";

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sources(path);
    return /\.tsx$/.test(name) && !/\.test\.tsx$/.test(name) ? [path] : [];
  });
}

const title = "عنوان";

describe("SectionTitle", () => {
  it("renders a heading of the requested level with an icon and the title", () => {
    const { container } = render(<SectionTitle level={3} icon={Sun} id="t">{title}</SectionTitle>);
    expect(screen.getByRole("heading", { level: 3, name: title })).toHaveAttribute("id", "t");
    expect(container.querySelector("svg[aria-hidden='true']")).not.toBeNull();
  });

  it("is the only way features render h2/h3 (no raw headings under features/)", () => {
    const offenders = sources(join(__dirname, "..", "..", "features")).filter((file) => /<h[23][\s>]/.test(readFileSync(file, "utf8")));
    expect(offenders).toEqual([]);
  });
});
