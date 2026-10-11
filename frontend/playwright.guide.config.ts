import { defineConfig, devices } from "@playwright/test";

// The user-guide capture (tools/Build-UserGuide.ps1): drives the published program on a temporary database and saves the screenshots of the guide.
// Not part of the normal test run (the normal config only looks in ./e2e).
export default defineConfig({
  testDir: "./guide",
  fullyParallel: false,
  workers: 1,
  reporter: "list",
  timeout: 900_000,
  use: { ...devices["Desktop Chrome"], headless: true, viewport: { width: 1180, height: 760 }, locale: "ar" },
});
