import { describe, expect, it } from "vitest";
import { queryClient } from "./queryClient";

describe("queryClient", () => {
  it("never pauses requests because the browser reports no internet (the program only talks to 127.0.0.1)", () => {
    const defaults = queryClient.getDefaultOptions();
    expect(defaults.queries?.networkMode).toBe("always");
    expect(defaults.mutations?.networkMode).toBe("always");
  });
});
