import { mkdirSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, goToSection, setupOwner } from "./support/flows";

const server = new ApiServer();
const text = messages.school.backup;

test("(MF10) backup folder chosen with the in-app picker, restore chosen from the list of backups", async ({ browser, page }, testInfo) => {
  test.setTimeout(120_000);
  await server.start(browser, "mf-backup");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Backup-Owner-1");
    await goToSection(page, messages.school.nav.settings);

    // A folder under the test's own output folder (never the owner's Documents), reached through the picker.
    const root = testInfo.outputPath("backup-root");
    mkdirSync(join(root, "نسخ المدرسة"), { recursive: true });
    mkdirSync(join(root, "ذاكرة خارجية"), { recursive: true });
    await page.getByText(text.typed).click();
    await page.getByLabel(text.typedLabel).fill(root);
    await expect(page.locator("#backup-folder")).toHaveText(root);

    await page.getByRole("button", { name: text.chooseFolder }).click();
    const picker = page.getByRole("dialog", { name: text.picker.title });
    await expect(picker.locator(".folder-path")).toHaveText(root);
    await expect(picker.getByRole("button", { name: "نسخ المدرسة" })).toBeVisible();
    await expect(picker.getByRole("button", { name: "ذاكرة خارجية" })).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath("mf10-picker.png"), fullPage: true });
    await expectNoSeriousA11yViolations(page, "folder picker");
    await picker.getByRole("button", { name: "نسخ المدرسة" }).click();
    await expect(picker.getByText(text.picker.noSubfolders)).toBeVisible();
    // Up, then the starting places (drives, documents, desktop), then back into the chosen folder through the typed path.
    await picker.getByRole("button", { name: text.picker.up }).click();
    await expect(picker.locator(".folder-path")).toHaveText(root);
    await picker.getByRole("button", { name: "نسخ المدرسة" }).click();
    await picker.getByRole("button", { name: text.picker.choose }).click();
    await expect(page.getByRole("dialog", { name: text.picker.title })).toBeHidden();
    await expect(page.locator("#backup-folder")).toHaveText(join(root, "نسخ المدرسة"));

    // No backups yet in that folder: an empty list, and restore cannot be started.
    await expect(page.getByText(text.list.empty)).toBeVisible();
    await expect(page.getByRole("button", { name: text.restore, exact: true })).toBeDisabled();

    await page.getByRole("button", { name: text.create }).click();
    await expect(page.getByRole("status").filter({ hasText: "timetable-backup-" })).toBeVisible();
    const table = page.getByRole("table", { name: text.list.title });
    await expect(table.locator("tbody tr")).toHaveCount(1);
    await expect(table.getByText(text.list.kinds.manual)).toBeVisible();
    await expect(table.getByText(text.list.restorable)).toBeVisible();
    await expectNoSeriousA11yViolations(page, "settings with backup list");
    for (const width of [375, 1024, 1920]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `backup section at ${width}px`);
    }
    await page.setViewportSize({ width: 1366, height: 900 });

    // Choose the backup from the list; the two confirmations are still required.
    await table.getByRole("button", { name: text.list.choose }).click();
    await expect(table.getByRole("button", { name: text.list.chosen })).toHaveAttribute("aria-pressed", "true");
    await expect(page.getByRole("button", { name: text.restore, exact: true })).toBeDisabled();
    await page.getByLabel(text.confirmReplace).check();
    await page.screenshot({ path: testInfo.outputPath("mf10-restore-list.png"), fullPage: true });
    await page.getByRole("button", { name: text.restore, exact: true }).click();
    await page.getByRole("dialog", { name: text.restoreConfirmTitle }).getByRole("button", { name: text.restore }).click();
    await expect(page.getByRole("status").filter({ hasText: "pre-restore-" })).toBeVisible();
  } finally {
    await server.stop();
  }
});
