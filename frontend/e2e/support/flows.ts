import AxeBuilder from "@axe-core/playwright";
import { expect, type Locator, type Page } from "@playwright/test";
import { messages } from "../../src/i18n/messages";

export const breakpoints = [375, 768, 1024, 1440] as const;

/** First-run setup through the real UI, acknowledging the recovery code and postponing the wizard; ends on the dashboard. */
export async function setupOwner(page: Page, baseUrl: string, username: string, password: string): Promise<void> {
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: messages.app.setupTitle })).toBeVisible();
  await page.getByLabel(messages.app.username).fill(username);
  await page.getByLabel(messages.app.password, { exact: true }).fill(password);
  await page.getByLabel(messages.app.confirmPassword).fill(password);
  await page.getByRole("button", { name: messages.app.createAccount }).click();
  await page.getByLabel(messages.app.confirmCodeSaved).check();
  await page.getByRole("button", { name: messages.app.continue }).click();
  // A fresh account opens the setup wizard once (spec 2.5 §5); these flows leave it for later.
  await expect(page.getByRole("heading", { name: messages.school.wizard.title, level: 1 })).toBeVisible();
  await page.getByRole("link", { name: messages.school.wizard.later }).click();
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

const nav = messages.school.nav;

/** Screens that are tabs inside a sidebar group (spec 2.5 §2.6): tab label -> group label. */
const tabGroups: Readonly<Record<string, string>> = {
  [nav.profile]: nav.school,
  [nav.academicYears]: nav.school,
  [nav.scheduleStructure]: nav.school,
  [nav.calendar]: nav.school,
  [nav.stagesSections]: nav.classes,
  [nav.subjects]: nav.classes,
  [nav.curriculum]: nav.classes,
};

/** Opens a screen through the sidebar, and through its group's tab when the screen is a tab. */
export async function goToSection(page: Page, label: string): Promise<void> {
  const group = tabGroups[label];
  await page.getByRole("navigation", { name: nav.sidebarLabel }).getByRole("link", { name: group ?? label }).click();
  if (group) await page.getByRole("navigation", { name: group }).getByRole("link", { name: label }).click();
  await expect(page.getByRole("heading", { name: label, level: 1 })).toBeVisible();
}

const dateParts = messages.app.dateParts;
const timeParts = messages.app.timeParts;

/** Types an ISO date into a DateField (day, month, year segments). */
export async function fillDate(scope: Page | Locator, label: string, iso: string): Promise<void> {
  const [year, month, day] = iso.split("-");
  await scope.getByRole("textbox", { name: `${label} - ${dateParts.day}`, exact: true }).fill(day);
  await scope.getByRole("textbox", { name: `${label} - ${dateParts.month}`, exact: true }).fill(month);
  await scope.getByRole("textbox", { name: `${label} - ${dateParts.year}`, exact: true }).fill(year);
}

/** Types "HH:mm" into a TimeField. */
export async function fillTime(scope: Page | Locator, label: string, value: string): Promise<void> {
  const [hours, minutes] = value.split(":");
  await scope.getByRole("textbox", { name: `${label} - ${timeParts.hours}`, exact: true }).fill(hours);
  await scope.getByRole("textbox", { name: `${label} - ${timeParts.minutes}`, exact: true }).fill(minutes);
}

/** Add pattern rule: dialogs are centred (never anchored to an edge) and need no inner scrolling at 1280x720. */
export async function expectCenteredDialog(page: Page, dialog: Locator, name: string): Promise<void> {
  const viewport = page.viewportSize();
  await page.setViewportSize({ width: 1280, height: 720 });
  const box = await dialog.boundingBox();
  expect(box, `${name} is visible`).not.toBeNull();
  if (box) {
    const left = box.x;
    const right = 1280 - (box.x + box.width);
    expect(Math.abs(left - right), `${name} is horizontally centred`).toBeLessThanOrEqual(2);
    expect(box.width, `${name} width`).toBeGreaterThanOrEqual(32 * 16 - 1);
    expect(box.width, `${name} width`).toBeLessThanOrEqual(40 * 16 + 1);
  }
  const scrolls = await dialog.evaluate((element) => element.scrollHeight > element.clientHeight + 1);
  expect(scrolls, `${name} scrolls inside at 1280x720`).toBe(false);
  if (viewport) await page.setViewportSize(viewport);
}

/** Spec 2.5 §2.3: no Latin letters in visible text, except data (the username) and image format codes. */
export async function expectNoLatinText(page: Page, screen: string, allowed: readonly string[] = []): Promise<void> {
  const visible = await page.locator("body").innerText();
  let text = visible.replace(/\b(?:PNG|JPEG|WebP)\b/g, "");
  for (const value of allowed) text = text.split(value).join("");
  const latin = text.match(/[A-Za-z]+/g) ?? [];
  expect(latin, `Latin text on ${screen}`).toEqual([]);
}

/** Spec 2.5 §2.1: a year range such as "2027 - 2028" is displayed in logical order (first year on the left). */
export async function expectYearInOrder(scope: Locator, first: string, second: string): Promise<void> {
  const ordered = await scope.evaluate((element, years) => {
    const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      const content = node.textContent ?? "";
      const a = content.indexOf(years[0]);
      const b = content.indexOf(years[1]);
      if (a < 0 || b < 0) continue;
      const box = (start: number, length: number) => {
        const range = document.createRange();
        range.setStart(node, start);
        range.setEnd(node, start + length);
        return range.getBoundingClientRect();
      };
      return box(a, years[0].length).left < box(b, years[1].length).left;
    }
    return null;
  }, [first, second] as const);
  expect(ordered, `${first} is displayed before ${second}`).toBe(true);
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
