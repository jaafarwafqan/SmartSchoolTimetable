import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { seedReadySchool as seedSchool } from "./support/readySchool";
import { breakpoints, expectNo24HourTimes, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, setupOwner } from "./support/flows";

const server = new ApiServer();
const generation = messages.school.generation;
const timetable = messages.school.timetable;
const seedReadySchool = (page: Page) => seedSchool(page, server.baseUrl);

test("(4) generate a timetable with real progress, read it three ways and approve it", async ({ browser, page }) => {
  test.setTimeout(240_000);
  await server.start(browser, "phase4-generation");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Generation-Owner-1");
    await seedReadySchool(page);
    await page.goto(`${server.baseUrl}/timetable/generate`);
    await expect(page.getByRole("heading", { name: generation.title, level: 1 })).toBeVisible();
    await expect(page.getByText(generation.readinessReady)).toBeVisible();
    await expectNoSeriousA11yViolations(page, "generation options");
    await page.getByLabel(generation.timeLimit).selectOption("10");
    await page.getByRole("button", { name: generation.start }).click();

    // Real progress: the stepper, then the result with the verifier badge (no fake percentage anywhere).
    await expect(page.getByRole("heading", { name: generation.resultTitle })).toBeVisible({ timeout: 90_000 });
    await expect(page.getByText(generation.statuses.completed)).toBeVisible();
    // MF3: plain Arabic first (headline, sentence, what could be better); the solver's numbers under «تفاصيل تقنية».
    await expect(page.getByText(generation.plain.ready, { exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: generation.plain.notesTitle })).toBeVisible();
    await expect(page.getByText(generation.verified)).toBeHidden();
    await page.getByText(generation.plain.technical).click();
    await expect(page.getByText(generation.verified)).toBeVisible();
    await expect(page.getByRole("table", { name: generation.scoreTitle })).toBeVisible();
    await expect(page.locator("main")).not.toContainText("%");
    await page.screenshot({ path: test.info().outputPath("mf3-generation-result.png"), fullPage: true });
    await expectNoSeriousA11yViolations(page, "generation result");
    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `generation at ${width}px`);
      await expectNoTextOverlap(page.locator("main"), `generation at ${width}px`);
    }
    await page.setViewportSize({ width: 1280, height: 900 });

    // Reopening the page shows the same finished run (state comes from the server, not the tab).
    await page.reload();
    await expect(page.getByText(generation.statuses.completed)).toBeVisible();

    await page.getByRole("link", { name: generation.openTimetable }).click();
    await expect(page.getByRole("heading", { name: timetable.title, level: 1 })).toBeVisible();
    await expect(page.getByText(timetable.verified)).toBeVisible();
    const grid = page.locator(".ui-tt-grid");
    await expect(grid).toHaveCount(1);
    // Keyboard: arrow keys move between cells.
    await grid.locator(".ui-tt-cell").first().focus();
    await page.keyboard.press("ArrowLeft");
    await expect(grid.locator("[data-cell=\"0:1\"] .ui-tt-cell")).toBeFocused();

    await expectNo24HourTimes(page, "section timetable");
    await expect(page.locator(".ui-tt-grid thead").first()).toContainText(" ص");
    await page.getByLabel(timetable.viewsLabel).selectOption("teacher");
    await expect(page.getByLabel(timetable.chooseTeacher)).toBeVisible();
    await expect(page.locator(".ui-tt-grid").first()).toBeVisible();
    await page.getByLabel(timetable.viewsLabel).selectOption("master");
    await expect(page.getByRole("table", { name: timetable.views.master })).toBeVisible();
    await expectNo24HourTimes(page, "master timetable");
    await expectNoSeriousA11yViolations(page, "timetable viewer");
    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `timetable viewer at ${width}px`);
    }
    await page.setViewportSize({ width: 1280, height: 900 });

    // MF4: the version is chosen from a compact bar at the top; editing hides «اعتماد» and shows «حفظ التعديلات» / «إلغاء».
    await expect(page.locator("#timetable-version")).toBeVisible();
    await page.getByRole("button", { name: timetable.edit }).click();
    await expect(page.getByRole("button", { name: timetable.approve })).toHaveCount(0);
    await expect(page.getByRole("button", { name: timetable.saveEdit })).toBeVisible();
    await page.getByRole("button", { name: timetable.cancelEdit }).click();
    await expect(page.getByRole("button", { name: timetable.approve })).toBeVisible();
    // The section grid is full width: every lesson visible without sideways scroll at 1366 and 1920; at 1024 the day column stays put.
    await page.getByLabel(timetable.viewsLabel).selectOption("section");
    for (const width of [1366, 1920]) {
      await page.setViewportSize({ width, height: 900 });
      const overflow = await page.locator(".timetable-main .ui-tt-grid-container").first().evaluate((element) => element.scrollWidth - element.clientWidth);
      expect(overflow, `section grid scrolls sideways at ${width}px`).toBeLessThanOrEqual(0);
      await page.screenshot({ path: test.info().outputPath(`mf4-timetable-${width}.png`), fullPage: true });
    }
    await page.setViewportSize({ width: 1024, height: 900 });
    expect(await page.locator(".timetable-main .ui-tt-grid tbody th").first().evaluate((element) => getComputedStyle(element).position)).toBe("sticky");
    await page.screenshot({ path: test.info().outputPath("mf4-timetable-1024.png"), fullPage: true });
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.getByRole("button", { name: timetable.approve }).click();
    await page.getByRole("dialog").getByRole("button", { name: timetable.approve }).click();
    await expect(page.getByText(timetable.approvedDone)).toBeVisible();
    await expect(page.locator(".version-bar .ui-badge", { hasText: timetable.approved })).toBeVisible();

    // Printing (M5): print media hides the shell and controls and shows the school header; the grid stays.
    await page.emulateMedia({ media: "print" });
    await expect(page.locator(".app-sidebar")).toBeHidden();
    await expect(page.locator(".timetable-controls").first()).toBeHidden();
    await expect(page.locator(".timetable-print-header")).toBeVisible();
    await expect(page.locator(".ui-tt-grid").first()).toBeVisible();
    await page.emulateMedia({ media: "screen" });
    await expect(page.locator(".timetable-print-header")).toBeHidden();
    // Excel (M5): the workbook downloads from the version.
    const download = page.waitForEvent("download");
    await page.getByRole("link", { name: timetable.exportExcel }).click();
    expect((await download).suggestedFilename()).toMatch(/^timetable-v\d+\.xlsx$/);

    // Manual edit (M4): moving the first lesson of a day into a free slot leaves a gap at lesson 1 (H3):
    // the verifier reports it at once, saving is blocked, and undo restores the timetable.
    await page.getByLabel(timetable.viewsLabel).selectOption("section");
    await page.getByRole("button", { name: timetable.edit }).click();
    await expect(page.getByRole("heading", { name: timetable.editTitle })).toBeVisible();
    await expect(page.getByText(timetable.noConflicts)).toBeVisible();
    const editor = page.locator(".timetable-editor .ui-tt-grid");
    const empty = editor.locator("td:has(.ui-tt-cell.is-empty)").first();
    const [row] = ((await empty.getAttribute("data-cell")) ?? "0:0").split(":");
    await editor.locator(`[data-cell="${row}:0"] .ui-tt-cell`).click();
    await empty.locator(".ui-tt-cell").click();
    await expect(page.locator(".timetable-conflicts")).toBeVisible();
    await expect(page.getByRole("button", { name: timetable.saveEdit })).toBeDisabled();
    await expectNoSeriousA11yViolations(page, "timetable editor with a conflict");
    await page.getByRole("button", { name: timetable.undo }).click();
    await expect(page.getByText(timetable.noConflicts)).toBeVisible();
    await expect(page.getByRole("button", { name: timetable.saveEdit })).toBeDisabled();
  } finally {
    await server.stop();
  }
});
