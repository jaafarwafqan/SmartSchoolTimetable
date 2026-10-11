import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  // CI runners are slower and shared: one worker and a longer per-test timeout there only (local runs unchanged).
  workers: process.env.CI ? 1 : undefined,
  reporter: "list",
  timeout: process.env.CI ? 240_000 : 120_000,
  expect: {
    // Screenshot checks (DESIGN_SYSTEM.md 12.5): allow tiny anti-aliasing differences only.
    toHaveScreenshot: { maxDiffPixelRatio: 0.0002, animations: "disabled", caret: "hide" },
  },
  use: {
    ...devices["Desktop Chrome"],
    headless: true,
  },
});
