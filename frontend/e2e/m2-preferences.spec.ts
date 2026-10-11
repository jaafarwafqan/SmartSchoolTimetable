import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { api } from "./support/api";
import { seedReadySchool } from "./support/readySchool";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, goToSection, setupOwner } from "./support/flows";

const server = new ApiServer();
const text = messages.school.preferences;
const printing = messages.school.printing;

async function chooseTheme(page: Page, label: string) {
  await page.locator(".ui-choice-card", { hasText: label }).click();
  await page.getByRole("button", { name: text.save }).click();
  await expect(page.getByRole("status").filter({ hasText: text.saved })).toBeVisible();
}

const themeOf = (page: Page) => page.locator("html").getAttribute("data-theme");

test("(M2) preferences: dark theme with AA contrast, persistence, digits, print defaults, default semester", async ({ browser, page }, testInfo) => {
  test.setTimeout(240_000);
  await server.start(browser, "m2-preferences");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Prefs-Owner-1");
    const yearId = await seedReadySchool(page, server.baseUrl);
    const run = await api<{ id: number }>(page, server.baseUrl, "POST", `/academic-years/${yearId}/generation/runs`, { timeLimitSeconds: 10, deterministic: true, seed: 5 });
    for (let attempt = 0; attempt < 200; attempt++) {
      const state = await api<{ status: string }>(page, server.baseUrl, "GET", `/generation/runs/${run.id}`);
      if (state.status === "completed") break;
      await page.waitForTimeout(500);
    }

    // Light by default; the section sits in Settings › عام.
    await goToSection(page, messages.school.nav.settings);
    await expect(page.getByRole("heading", { name: text.title, level: 2 })).toBeVisible();
    expect(await themeOf(page)).toBe("light");

    // Dark: chosen, saved, applied at once, remembered across a reload before the app paints.
    await chooseTheme(page, text.theme.dark);
    expect(await themeOf(page)).toBe("dark");
    await page.reload();
    expect(await themeOf(page)).toBe("dark");
    await expect(page.getByRole("heading", { name: text.title, level: 2 })).toBeVisible();
    await expectNoSeriousA11yViolations(page, "settings in the dark theme");
    await page.screenshot({ path: testInfo.outputPath("m2-settings-dark.png"), fullPage: true });
    for (const width of [375, 1024]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `dark settings at ${width}px`);
    }
    await page.setViewportSize({ width: 1366, height: 900 });

    // The other main screens keep AA contrast in the dark theme (axe checks the real computed colours).
    await page.goto(`${server.baseUrl}/`);
    await expect(page.getByRole("heading", { name: messages.school.nav.dashboard, level: 1 })).toBeVisible();
    await expectNoSeriousA11yViolations(page, "dashboard in the dark theme");
    await page.screenshot({ path: testInfo.outputPath("m2-dashboard-dark.png"), fullPage: true });
    await page.goto(`${server.baseUrl}/timetable/view`);
    await expect(page.locator(".ui-tt-grid").first()).toBeVisible();
    await expectNoSeriousA11yViolations(page, "timetable in the dark theme");
    await page.screenshot({ path: testInfo.outputPath("m2-timetable-dark.png"), fullPage: true });
    await page.goto(`${server.baseUrl}/school/calendar`);
    await expectNoSeriousA11yViolations(page, "calendar in the dark theme");
    await page.goto(`${server.baseUrl}/setup`);
    await expectNoSeriousA11yViolations(page, "setup wizard in the dark theme");

    // «حسب نظام التشغيل» follows the system, and a fixed light theme ignores it.
    await goToSection(page, messages.school.nav.settings);
    await chooseTheme(page, text.theme.system);
    await page.emulateMedia({ colorScheme: "dark" });
    await expect.poll(() => themeOf(page)).toBe("dark");
    await page.emulateMedia({ colorScheme: "light" });
    await expect.poll(() => themeOf(page)).toBe("light");
    await chooseTheme(page, text.theme.light);
    await page.emulateMedia({ colorScheme: "dark" });
    expect(await themeOf(page)).toBe("light");
    await page.emulateMedia({ colorScheme: "light" });

    // Digits: Latin on every screen, then back.
    await page.getByLabel(text.digits.label).selectOption("western");
    await page.getByRole("button", { name: text.save }).click();
    await expect(page.getByRole("status").filter({ hasText: text.saved })).toBeVisible();
    await page.goto(`${server.baseUrl}/`);
    await expect(page.locator(".count-value").first()).toHaveText(/^[0-9]+$/);
    await goToSection(page, messages.school.nav.settings);
    await page.getByLabel(text.digits.label).selectOption("arabicIndic");
    await page.getByRole("button", { name: text.save }).click();
    await expect(page.getByRole("status").filter({ hasText: text.saved })).toBeVisible();

    // Print defaults: the section job A3 portrait, the teacher job A4 landscape; a change in the print panel stays for that print only.
    await page.locator("#pref-section-paper").selectOption("a3");
    await page.locator("#pref-section-orientation").selectOption("portrait");
    await page.locator("#pref-teacher-orientation").selectOption("landscape");
    await page.getByRole("button", { name: text.save }).click();
    await expect(page.getByRole("status").filter({ hasText: text.saved })).toBeVisible();
    await page.goto(`${server.baseUrl}/timetable/view`);
    await page.getByText(printing.title).click();
    await expect(page.locator("#print-paper")).toHaveValue("A3");
    await expect(page.locator("#print-orientation")).toHaveValue("portrait");
    await page.locator("#print-scope").selectOption("teachers");
    await expect(page.locator("#print-paper")).toHaveValue("A4");
    await expect(page.locator("#print-orientation")).toHaveValue("landscape");
    await page.locator("#print-paper").selectOption("A3");
    await expect(page.locator("#print-paper")).toHaveValue("A3");
    await page.reload();
    await page.getByText(printing.title).click();
    await expect(page.locator("#print-paper")).toHaveValue("A3"); // the section default again, not the one-off A3 of the teacher job
    await expect(page.locator("#print-orientation")).toHaveValue("portrait");

    // The preferences are stored on the server (versioned), not in this browser.
    const stored = await api<{ theme: string; section: { paper: string; orientation: string }; teacher: { orientation: string }; version: number }>(page, server.baseUrl, "GET", "/preferences/");
    expect([stored.theme, stored.section.paper, stored.section.orientation, stored.teacher.orientation]).toEqual(["light", "a3", "portrait", "landscape"]);
    expect(stored.version).toBeGreaterThan(1);
  } finally {
    await server.stop();
  }
});
