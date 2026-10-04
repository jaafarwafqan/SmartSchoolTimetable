import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";
import { messages } from "../../src/i18n/messages";

export const breakpoints = [375, 768, 1024, 1440] as const;

/** First-run setup through the real UI, acknowledging the recovery code; ends on the dashboard. */
export async function setupOwner(page: Page, baseUrl: string, username: string, password: string): Promise<void> {
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: messages.app.setupTitle })).toBeVisible();
  await page.getByLabel(messages.app.username).fill(username);
  await page.getByLabel(messages.app.password, { exact: true }).fill(password);
  await page.getByLabel(messages.app.confirmPassword).fill(password);
  await page.getByRole("button", { name: messages.app.createAccount }).click();
  await page.getByLabel(messages.app.confirmCodeSaved).check();
  await page.getByRole("button", { name: messages.app.continue }).click();
  await expect(page.getByRole("heading", { name: messages.school.nav.dashboard, level: 1 })).toBeVisible();
}

export async function openUserMenuItem(page: Page, item: string): Promise<void> {
  await page.getByRole("button", { name: messages.school.shell.userMenu }).click();
  await page.getByRole("menuitem", { name: item }).click();
}

export async function logout(page: Page): Promise<void> {
  await openUserMenuItem(page, messages.app.logout);
  await expect(page.getByRole("heading", { name: messages.app.loginTitle })).toBeVisible();
}

export async function goToSection(page: Page, label: string): Promise<void> {
  await page.getByRole("navigation", { name: messages.school.nav.sidebarLabel }).getByRole("link", { name: label }).click();
  await expect(page.getByRole("heading", { name: label, level: 1 })).toBeVisible();
}

export async function expectNoSeriousA11yViolations(page: Page, screen: string): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .analyze();
  const blocking = results.violations
    .filter((violation) => violation.impact === "serious" || violation.impact === "critical")
    .map((violation) => `${violation.id} (${violation.impact}): ${violation.nodes.map((node) => node.target.join(" ")).join(" | ")}`);
  expect(blocking, `axe violations on ${screen}`).toEqual([]);
}

/** Screenshot checks at the four reference widths, also asserting no horizontal page scroll. */
export async function expectBreakpointScreenshots(page: Page, name: string): Promise<void> {
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

/** A valid 1x1 PNG, so uploaded logos really decode in the browser. */
export const tinyPng = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=",
  "base64",
);
