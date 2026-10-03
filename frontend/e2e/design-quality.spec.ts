import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";

// DESIGN_SYSTEM.md 12.5: axe on key screens and screenshots at the four reference widths.
const server = new ApiServer();
const breakpoints = [375, 768, 1024, 1440] as const;
const password = "Design-Check-1";

test.describe.configure({ mode: "serial" });

test.beforeAll(async ({ browser }) => {
  await server.start(browser, "design-quality");
});

test.afterAll(async () => {
  await server.stop();
});

async function expectNoSeriousA11yViolations(page: Page, screen: string): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .analyze();
  const blocking = results.violations
    .filter((violation) => violation.impact === "serious" || violation.impact === "critical")
    .map((violation) => `${violation.id} (${violation.impact}): ${violation.nodes.map((node) => node.target.join(" ")).join(" | ")}`);
  expect(blocking, `axe violations on ${screen}`).toEqual([]);
}

async function expectBreakpointScreenshots(page: Page, name: string): Promise<void> {
  for (const width of breakpoints) {
    await page.setViewportSize({ width, height: 900 });
    await page.evaluate(async () => {
      await document.fonts.ready;
      window.scrollTo(0, 0);
    });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, `${name} scrolls horizontally at ${width}px`).toBeLessThanOrEqual(0);
    await expect(page).toHaveScreenshot(`${name}-${width}.png`, { fullPage: true });
  }
  await page.setViewportSize({ width: 1280, height: 900 });
}

async function acknowledgeCode(page: Page): Promise<void> {
  await page.getByLabel(messages.app.confirmCodeSaved).check();
  await page.getByRole("button", { name: messages.app.continue }).click();
}

test("account screens pass axe and keep their layout at 375, 768, 1024 and 1440 px", async ({ page }) => {
  await page.goto(server.baseUrl);
  await expect(page.getByRole("heading", { name: messages.app.setupTitle })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "setup");

  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill(password);
  await page.getByLabel(messages.app.confirmPassword).fill(password);
  await page.getByRole("button", { name: messages.app.createAccount }).click();
  await expect(page.getByRole("heading", { name: messages.app.codeTitle })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "recovery code");

  await page.reload();
  await expect(page.getByRole("heading", { name: messages.app.recoveryPendingTitle })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "recovery pending");
  await page.getByLabel(messages.app.currentPassword).fill(password);
  await page.getByRole("button", { name: messages.app.generateCode }).click();
  await acknowledgeCode(page);

  await page.getByRole("link", { name: messages.app.settings }).first().click();
  await expect(page.getByRole("heading", { name: messages.app.settings, level: 1 })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "settings");
  await expectBreakpointScreenshots(page, "settings");

  await page.getByRole("button", { name: messages.app.logout }).click();
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "login");
  await expectBreakpointScreenshots(page, "login");

  await page.getByRole("button", { name: messages.app.recoveryLink }).click();
  await expect(page.getByRole("heading", { name: messages.app.recoveryTitle })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "recovery form");
});
