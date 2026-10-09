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
  [nav.resources]: nav.classes,
  [nav.workload]: nav.teachers,
  [nav.schedulingProfile]: nav.settings,
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
/**
 * R1: no time on the page is on a 24-hour clock. Arabic-Indic digits are converted first, because `\d` in the
 * required pattern only matches ASCII digits and «١٣:٠٠» would otherwise slip through.
 */
export async function expectNo24HourTimes(page: Page, screen: string): Promise<void> {
  const text = (await page.locator("main").innerText()).replace(/[٠-٩]/g, (digit) => String(digit.charCodeAt(0) - 0x0660));
  expect(text.match(/\b(1[3-9]|2[0-3]):\d\d\b/g), `${screen} shows a 24-hour time`).toBeNull();
}

/** Sets a 12-hour TimeField from a 24-hour "HH:mm" value (hour 1–12, minute, ص/م selects). */
export async function fillTime(scope: Page | Locator, label: string, value: string): Promise<void> {
  const [hours, minutes] = value.split(":").map(Number);
  await scope.getByRole("combobox", { name: `${label} - ${timeParts.hours}`, exact: true }).selectOption(String(hours % 12 === 0 ? 12 : hours % 12));
  await scope.getByRole("combobox", { name: `${label} - ${timeParts.minutes}`, exact: true }).selectOption(String(minutes));
  await scope.getByRole("combobox", { name: `${label} - ${timeParts.meridiem}`, exact: true }).selectOption(hours < 12 ? "am" : "pm");
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
  await expect(scope).toContainText(second); // the rows may still be loading
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
/** `mask` hides values that change on every run (for example the server's check time). */
export async function expectBreakpointScreenshots(page: Page, name: string, mask: Locator[] = []): Promise<void> {
  for (const width of breakpoints) {
    await page.setViewportSize({ width, height: 900 });
    await page.evaluate(async () => {
      await document.fonts.ready;
      window.scrollTo(0, 0);
    });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, `${name} scrolls horizontally at ${width}px`).toBeLessThanOrEqual(0);
    await expect(page).toHaveScreenshot(`${name}-${width}.png`, { fullPage: true, mask });
  }
  await page.setViewportSize({ width: 1280, height: 900 });
}

/** A valid 1x1 PNG, so uploaded logos really decode in the browser. */
export const tinyPng = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=",
  "base64",
);

/** Fix B3: the page itself never scrolls sideways; only table containers may. */
export async function expectNoPageScrollX(page: Page, screen: string): Promise<void> {
  const [scrollWidth, clientWidth] = await page.evaluate(() => [document.documentElement.scrollWidth, document.documentElement.clientWidth]);
  expect(scrollWidth, `horizontal page scroll on ${screen} at ${clientWidth}px`).toBeLessThanOrEqual(clientWidth);
}

/**
 * Fix B4: fails when the boxes of two visible texts inside the container overlap. Texts are the innermost elements
 * that own non-empty text, measured per line box; an element and its own ancestors never count as overlapping.
 * Boxes touching by under 1px are tolerated (rounding).
 */
export async function expectNoTextOverlap(container: Locator, screen: string): Promise<void> {
  const overlaps = await container.evaluate((root) => {
    const owners = [...root.querySelectorAll<HTMLElement>("*")].filter((element) => {
      if (element.closest("[aria-hidden='true'], .sr-only")) return false;
      const details = element.closest("details");
      if (details && !details.open && !element.closest("summary")) return false; // closed disclosure content is not shown
      const ownText = [...element.childNodes].some((node) => node.nodeType === Node.TEXT_NODE && (node.textContent ?? "").trim() !== "");
      const style = getComputedStyle(element);
      return ownText && style.visibility !== "hidden" && style.display !== "none" && element.getClientRects().length > 0;
    });
    // One box per rendered line, trimmed to the line height: the glyph box of Arabic fonts is taller than the
    // line box, so untrimmed boxes of two stacked lines would "overlap" without any visible collision.
    const boxes = owners.flatMap((element) => {
      const style = getComputedStyle(element);
      const fontSize = parseFloat(style.fontSize);
      const lineHeight = style.lineHeight === "normal" ? fontSize * 1.2 : parseFloat(style.lineHeight);
      const range = document.createRange();
      range.selectNodeContents(element);
      return [...range.getClientRects()].filter((rect) => rect.width > 0 && rect.height > 0).map((rect) => {
        const half = Math.min(rect.height, lineHeight) / 2;
        const middle = (rect.top + rect.bottom) / 2;
        return { element, rect: { left: rect.left, right: rect.right, top: middle - half, bottom: middle + half } };
      });
    });
    // Text under an opaque layer (a sticky header, column or totals row over scrolled cells) is not visible, so
    // it cannot overlap visible text; text drawn over text with a transparent background still counts.
    const covered = (element: Element, rect: { left: number; right: number; top: number; bottom: number }) => {
      const top = document.elementFromPoint((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);
      if (!top || top === element || element.contains(top) || top.contains(element)) return false;
      for (let node: Element | null = top; node && !node.contains(element); node = node.parentElement) {
        const background = getComputedStyle(node).backgroundColor;
        if (background !== "rgba(0, 0, 0, 0)" && background !== "transparent") return true;
      }
      return false;
    };
    const visibleBoxes = boxes.filter(({ element, rect }) => !covered(element, rect));
    // At the centre of an overlap: if an opaque element (not containing the lower text) lies between the two texts
    // in the paint stack, the lower text is hidden there (e.g. rows scrolling under a sticky totals row).
    type Box = { left: number; right: number; top: number; bottom: number };
    const opaque = (node: Element) => !["rgba(0, 0, 0, 0)", "transparent"].includes(getComputedStyle(node).backgroundColor);
    const hiddenUnderOpaqueLayer = (one: Element, two: Element, a: Box, b: Box) => {
      const x = (Math.max(a.left, b.left) + Math.min(a.right, b.right)) / 2;
      const y = (Math.max(a.top, b.top) + Math.min(a.bottom, b.bottom)) / 2;
      const stack = document.elementsFromPoint(x, y);
      const indexOf = (target: Element) => stack.findIndex((node) => node === target || target.contains(node));
      const [first, second] = [indexOf(one), indexOf(two)];
      if (first < 0 || second < 0) return true; // at least one is not painted there at all
      const [upper, lower] = first <= second ? [first, two] : [second, one];
      return stack.slice(upper, Math.max(first, second)).some((node) => !node.contains(lower) && opaque(node));
    };
    const found: string[] = [];
    for (let a = 0; a < visibleBoxes.length; a++) {
      for (let b = a + 1; b < visibleBoxes.length; b++) {
        const first = visibleBoxes[a];
        const second = visibleBoxes[b];
        if (first.element === second.element || first.element.contains(second.element) || second.element.contains(first.element)) continue;
        const width = Math.min(first.rect.right, second.rect.right) - Math.max(first.rect.left, second.rect.left);
        const height = Math.min(first.rect.bottom, second.rect.bottom) - Math.max(first.rect.top, second.rect.top);
        if (width > 1 && height > 1 && hiddenUnderOpaqueLayer(first.element, second.element, first.rect, second.rect)) continue;
        if (width > 1 && height > 1) found.push(`"${first.element.textContent?.trim().slice(0, 30)}" / "${second.element.textContent?.trim().slice(0, 30)}" (${Math.round(width)}x${Math.round(height)})`);
      }
    }
    return found;
  });
  expect(overlaps, `overlapping texts on ${screen}`).toEqual([]);
}
