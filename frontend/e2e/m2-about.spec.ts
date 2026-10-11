import { readFileSync } from "node:fs";
import { userInfo } from "node:os";
import { expect, test } from "@playwright/test";
import { contactLinks } from "../src/features/about/contactLinks";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

const server = new ApiServer();
const text = messages.school.about;

test("(M2) «حول البرنامج»: reached from the user menu, shows version, licence, data folder and third-party licences; the support log is redacted", async ({ browser, page }, testInfo) => {
  test.setTimeout(120_000);
  await server.start(browser, "m2-about");
  try {
    await setupOwner(page, server.baseUrl, "owner", "About-Owner-1");

    // The footer carries the version on every screen.
    await expect(page.locator(".app-footer")).toContainText(text.facts.version);

    await page.getByRole("button", { name: messages.school.shell.userMenu }).click();
    await page.getByRole("menuitem", { name: text.menu }).click();
    await expect(page.getByRole("heading", { name: text.title, level: 1 })).toBeVisible();
    await expect(page.getByRole("navigation", { name: messages.school.nav.settings }).getByRole("link", { name: messages.school.nav.settingsAbout })).toBeVisible();

    await expect(page.getByRole("heading", { name: text.appName })).toBeVisible();
    await expect(page.getByText(text.facts.buildDate)).toBeVisible();
    await expect(page.getByText(text.facts.developerName, { exact: true })).toBeVisible();
    await expect(page.getByText("جميع الحقوق محفوظة")).toBeVisible();
    await expect(page.getByRole("button", { name: text.dataFolder.open })).toBeVisible();
    // The contact section shows the number and the user name as text and as links (outside the program).
    await expect(page.getByRole("heading", { name: text.contact.title })).toBeVisible();
    await expect(page.getByRole("link", { name: contactLinks.whatsappValue })).toHaveAttribute("href", contactLinks.whatsappLink);
    await expect(page.getByRole("link", { name: contactLinks.telegramValue })).toHaveAttribute("href", contactLinks.telegramLink);
    const table = page.getByRole("table", { name: text.thirdParty.title });
    await expect(table.getByText("Google OR-Tools")).toBeVisible();
    await expectNoSeriousA11yViolations(page, "about");
    await page.screenshot({ path: testInfo.outputPath("about-1366.png"), fullPage: true });
    for (const width of [375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `about at ${width}px`);
    }
    await page.setViewportSize({ width: 375, height: 900 });
    await page.screenshot({ path: testInfo.outputPath("about-375.png"), fullPage: true });

    // The support log downloads as a text file with the facts and without this computer's user name.
    const [download] = await Promise.all([page.waitForEvent("download"), page.getByRole("link", { name: text.support.export }).click()]);
    expect(download.suggestedFilename()).toMatch(/^support-log-\d{8}-\d{6}\.txt$/);
    const path = testInfo.outputPath("support-log.txt");
    await download.saveAs(path);
    const content = readFileSync(path, "utf8");
    expect(content).toContain("Application version:");
    expect(content).toContain("Row counts per table");
    expect(content.toLowerCase()).not.toContain(userInfo().username.toLowerCase());
    expect(content).not.toContain("About-Owner-1");
  } finally {
    await server.stop();
  }
});
