import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { expectBreakpointScreenshots, expectNoSeriousA11yViolations, goToSection, logout } from "./support/flows";

// DESIGN_SYSTEM.md 12.5: axe on key screens and screenshots at the four reference widths.
const server = new ApiServer();
const password = "Design-Check-1";

test.describe.configure({ mode: "serial" });

test.beforeAll(async ({ browser }) => {
  await server.start(browser, "design-quality");
});

test.afterAll(async () => {
  await server.stop();
});

async function acknowledgeCode(page: Page): Promise<void> {
  await page.getByLabel(messages.app.confirmCodeSaved).check();
  await page.getByRole("button", { name: messages.app.continue }).click();
}

test("account screens pass axe and keep their layout at 375, 768, 1024 and 1440 px", async ({ page }, testInfo) => {
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

  await goToSection(page, messages.school.nav.settings);
  // The suggested backup folder is under the machine's user profile and may already hold backups, so the list (and the layout) would
  // depend on the computer: point it at an empty folder of this test instead, and mask the path text.
  await page.getByText(messages.school.backup.typed).click();
  await page.getByLabel(messages.school.backup.typedLabel).fill(testInfo.outputPath("empty-backups"));
  await expect(page.getByText(messages.school.backup.list.empty)).toBeVisible();
  await page.getByText(messages.school.backup.typed).click(); // collapse the fallback again: its input shows a slice of this computer's path
  await expectNoSeriousA11yViolations(page, "settings");
  await expectBreakpointScreenshots(page, "settings", [page.locator("#backup-folder")]);

  await logout(page);
  await expectNoSeriousA11yViolations(page, "login");
  await expectBreakpointScreenshots(page, "login");

  await page.getByRole("button", { name: messages.app.recoveryLink }).click();
  await expect(page.getByRole("heading", { name: messages.app.recoveryTitle })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "recovery form");
});
