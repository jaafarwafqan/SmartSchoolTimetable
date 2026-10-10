import { describe, expect, it } from "vitest";
import { resolveTheme } from "./theme";

describe("theme preference", () => {
  it("follows the system only for «حسب النظام»", () => {
    expect(resolveTheme("system", true)).toBe("dark");
    expect(resolveTheme("system", false)).toBe("light");
  });

  it("keeps an explicit choice whatever the system says", () => {
    expect(resolveTheme("light", true)).toBe("light");
    expect(resolveTheme("dark", false)).toBe("dark");
  });
});
