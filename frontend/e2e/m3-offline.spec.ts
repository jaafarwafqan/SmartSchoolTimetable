import { expect, test } from "@playwright/test";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { seedReadySchool } from "./support/readySchool";
import { setupOwner } from "./support/flows";

// M3 offline proof: the program works with no internet. Every request the browser makes while the owner uses the main screens goes to the local
// server; anything else would be blocked here and fails the test. The Arabic font, the scripts and the styles are all served locally.
const server = new ApiServer();

test("(M3) offline: every request goes to 127.0.0.1, nothing is blocked, the fonts are local", async ({ browser, page }) => {
  test.setTimeout(240_000);
  await server.start(browser, "m3-offline");
  const external: string[] = [];
  const failed: string[] = [];
  const local = new URL(server.baseUrl).host;
  await page.route("**/*", async (route) => {
    const url = new URL(route.request().url());
    if (url.host === local || url.protocol === "data:" || url.protocol === "blob:") return route.continue();
    external.push(route.request().url());
    return route.abort();
  });
  page.on("requestfailed", (request) => failed.push(`${request.url()} ${request.failure()?.errorText ?? ""}`));
  try {
    await setupOwner(page, server.baseUrl, "owner", "Offline-Owner-1");
    await seedReadySchool(page, server.baseUrl);
    for (const path of ["/", "/school/profile", "/school/year", "/classes/stages", "/teachers/list", "/teachers/workload", "/timetable/generate", "/settings/general", "/settings/backup", "/about"]) {
      await page.goto(`${server.baseUrl}${path}`);
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
      await page.waitForLoadState("networkidle");
    }
    expect(external, "requests to anything but the local server").toEqual([]);
    expect(failed.filter((entry) => !entry.includes("net::ERR_ABORTED")), "failed requests").toEqual([]);

    // The Arabic UI font is a local file (no web font service) and it is really used.
    const fonts = await page.evaluate(async () => {
      await document.fonts.ready;
      return [...document.fonts].filter((face) => face.status === "loaded").map((face) => face.family);
    });
    expect(fonts.some((family) => family.includes("Noto Sans Arabic"))).toBe(true);
    const fontSources = await page.evaluate(() => [...document.styleSheets].flatMap((sheet) => {
      try { return [...sheet.cssRules].filter((rule) => rule instanceof CSSFontFaceRule).map((rule) => (rule as CSSFontFaceRule).style.getPropertyValue("src")); } catch { return []; }
    }));
    expect(fontSources.length).toBeGreaterThan(0);
    expect(fontSources.every((source) => !/https?:\/\//.test(source))).toBe(true);
  } finally {
    await server.stop();
  }
});

test("(M3) the program keeps working when the computer loses its internet connection (the browser reports offline)", async ({ browser, page }) => {
  test.setTimeout(180_000);
  await server.start(browser, "m3-offline-event");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Offline-Event-1");
    await seedReadySchool(page, server.baseUrl);
    await page.goto(`${server.baseUrl}/`);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    // The internet is cut: the browser fires «offline» and navigator.onLine becomes false. The local server is still reachable.
    await page.evaluate(() => {
      Object.defineProperty(window.navigator, "onLine", { configurable: true, get: () => false });
      window.dispatchEvent(new Event("offline"));
    });
    // Screens opened from now on (new queries) and saving something (a mutation) still work.
    await page.getByRole("link", { name: messages.school.nav.teachers }).first().click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await page.waitForLoadState("networkidle");
    await expect(page.locator(".app-main").getByRole("alert")).toHaveCount(0);
    await page.getByRole("link", { name: messages.school.nav.settings }).first().click();
    await expect(page.getByRole("button", { name: messages.school.preferences.save })).toBeVisible({ timeout: 15_000 });
    await page.getByRole("button", { name: messages.school.preferences.save }).click();
    await expect(page.getByRole("status").filter({ hasText: messages.school.preferences.saved })).toBeVisible({ timeout: 15_000 });
  } finally {
    await server.stop();
  }
});

test("(M3) offline: the built program names no internet address except XML namespaces and error-message links", () => {
  const dist = resolve(dirname(fileURLToPath(import.meta.url)), "../../src/SmartSchoolTimetable.Api/wwwroot");
  const files = (folder: string): string[] => readdirSync(folder, { withFileTypes: true }).flatMap((entry) => (entry.isDirectory() ? files(join(folder, entry.name)) : [join(folder, entry.name)]));
  const allowed = [/^https?:\/\/(www\.)?w3\.org\//, /^https?:\/\/react\.dev\//, /^https?:\/\/reactjs\.org\//, /^https?:\/\/127\.0\.0\.1/, /^https?:\/\/localhost/, /^https?:\/\/reactrouter\.com\//, /^https?:\/\/github\.com\/remix-run\//, /^https?:\/\/mui\.com\//, /^https?:\/\/github\.com\/ungap\//, /^https?:\/\/tailwindcss\.com/,
    // The two contact links of «حول البرنامج» (WhatsApp, Telegram): links the owner clicks, never requested by the program itself.
    /^https:\/\/wa\.me\/9647806421980$/, /^https:\/\/t\.me\/jaafar_Wafqan$/];
  const offenders: string[] = [];
  for (const file of files(dist).filter((name) => /\.(js|css|html)$/.test(name))) {
    for (const match of readFileSync(file, "utf8").matchAll(/https?:\/\/[^\s"'`)<>\\]+/g)) {
      if (!allowed.some((pattern) => pattern.test(match[0]))) offenders.push(`${file.slice(dist.length)}: ${match[0]}`);
    }
  }
  expect(offenders).toEqual([]);
});
