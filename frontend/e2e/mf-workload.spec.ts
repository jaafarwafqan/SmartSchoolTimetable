import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { api } from "./support/api";
import { seedReadySchool } from "./support/readySchool";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

const server = new ApiServer();
const text = messages.school.workloadTable;
const wizard = messages.school.wizard;

type Progress = { version: number };

test("(MF2) the wizard's workload step: one table, a teacher per row, filters, statuses, suggestions for empty rows and loads", async ({ browser, page }, testInfo) => {
  test.setTimeout(180_000);
  await server.start(browser, "mf-workload");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Workload-Owner-1");
    await seedReadySchool(page, server.baseUrl);
    const progress = await api<Progress>(page, server.baseUrl, "GET", "/setup-progress/");
    await api(page, server.baseUrl, "PUT", "/setup-progress/", { currentStep: 7, completedSteps: [1, 2, 3, 4, 5, 6], skippedSteps: [], isFinished: false, version: progress.version });

    await page.goto(`${server.baseUrl}/setup`);
    await expect(page.getByRole("heading", { level: 2, name: wizard.steps[7] })).toBeVisible();
    // Two sections × four subjects = 8 rows, 56 weekly lessons, all assigned by the seed's suggester.
    await expect(page.getByText(text.summary("٨", "٨", "٥٦", "٥٦"))).toBeVisible();
    const table = page.getByRole("table", { name: text.table });
    await expect(table.locator("tbody tr")).toHaveCount(8);
    await expect(table.getByText(text.statuses.assigned)).toHaveCount(8);

    // Clear one row: its status says so (icon and text) and the summary follows.
    const first = table.locator("tbody tr").first();
    await first.getByRole("combobox").selectOption({ label: text.noTeacher });
    await expect(page.getByRole("status").filter({ hasText: text.saved })).toBeVisible();
    await expect(first).toContainText(text.statuses.missing);
    await expect(page.getByText(text.summary("٧", "٨", "٥٠", "٥٦"))).toBeVisible();

    // Filters: «بدون معلم» shows only that row; a stage filter keeps both sections of the stage.
    await page.locator("#workload-teacher-filter").selectOption({ label: text.unassigned });
    await expect(table.locator("tbody tr")).toHaveCount(1);
    await page.locator("#workload-teacher-filter").selectOption({ label: text.allTeachers });
    await expect(table.locator("tbody tr")).toHaveCount(8);

    // «اقتراح تلقائي» fills the empty row only and changes no existing assignment.
    await page.getByText(text.suggestTitle).click();
    await page.getByRole("button", { name: messages.school.workload.suggester.preview }).click();
    await page.getByRole("button", { name: messages.school.workload.suggester.apply }).click();
    await page.getByRole("dialog").getByRole("button", { name: messages.school.workload.suggester.apply }).click();
    await expect(page.getByText(text.summary("٨", "٨", "٥٦", "٥٦"))).toBeVisible();

    await expect(page.getByRole("table", { name: text.loadsTitle }).locator("tbody tr")).toHaveCount(5);
    await expectNoSeriousA11yViolations(page, "wizard workload table");
    for (const width of [1024, 1366, 1920]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `workload table at ${width}px`);
    }
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.screenshot({ path: testInfo.outputPath("mf2-workload-step.png"), fullPage: true });
  } finally {
    await server.stop();
  }
});
