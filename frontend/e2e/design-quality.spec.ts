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

  await goToSection(page, messages.school.nav.settings);
  await expectNoSeriousA11yViolations(page, "settings");
  // The suggested backup folder is under the machine's user profile: masked so the baseline does not depend on it.
  await expectBreakpointScreenshots(page, "settings", [page.locator("#backup-folder")]);

  await logout(page);
  await expectNoSeriousA11yViolations(page, "login");
  await expectBreakpointScreenshots(page, "login");

  await page.getByRole("button", { name: messages.app.recoveryLink }).click();
  await expect(page.getByRole("heading", { name: messages.app.recoveryTitle })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "recovery form");
});
