import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  reporter: "list",
  timeout: 120_000,
  expect: {
    // Screenshot checks (DESIGN_SYSTEM.md 12.5): allow tiny anti-aliasing differences only.
    toHaveScreenshot: { maxDiffPixelRatio: 0.0002, animations: "disabled", caret: "hide" },
  },
  use: {
    ...devices["Desktop Chrome"],
    headless: true,
  },
});
