import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { moveOrSwap } from "../src/features/timetable/editing";
import type { GridLesson, Timetable, TimetableCheck, TimetableVersionSummary } from "../src/features/timetable/timetableApi";
import { ApiServer } from "./support/apiServer";
import { api } from "./support/api";
import { seedReadySchool } from "./support/readySchool";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

const server = new ApiServer();
const generation = messages.school.generation;
const timetable = messages.school.timetable;
const lifecycle = messages.school.lifecycle;
const audit = messages.school.audit;

type Swap = { first: GridLesson; second: GridLesson };

/** A swap of two lessons of the first section that the independent verifier accepts (found through the real check endpoint). */
async function findValidSwap(page: Page, versionId: number): Promise<{ swap: Swap; data: Timetable }> {
  const data = await api<Timetable>(page, server.baseUrl, "GET", `/timetables/${versionId}`);
  const section = data.sections[0];
  const lessons = data.lessons.filter((lesson) => lesson.sectionId === section.id);
  for (const first of lessons) {
    for (const second of lessons) {
      if (first.day !== second.day || first.lesson >= second.lesson || first.lineId === second.lineId) continue;
      const edited = moveOrSwap(data.lessons, { sectionId: section.id, day: first.day, lesson: first.lesson }, { sectionId: section.id, day: second.day, lesson: second.lesson });
      const check = await api<TimetableCheck>(page, server.baseUrl, "POST", `/timetables/${versionId}/check`, { lessons: edited, note: null });
      if (check.violations.length === 0) return { swap: { first, second }, data };
    }
  }
  throw new Error("The test school has no valid swap.");
}

/** Clicks the two lessons of a swap in the manual editor (cells are addressed by day row and lesson column). */
async function swapInEditor(page: Page, data: Timetable, swap: Swap) {
  const editor = page.locator(".timetable-editor .ui-tt-grid");
  const cell = (lesson: GridLesson) => editor.locator(`[data-cell="${data.days.indexOf(lesson.day)}:${lesson.lesson - 1}"] .ui-tt-cell`);
  await cell(swap.first).click();
  await cell(swap.second).click();
}

async function generate(page: Page) {
  await page.goto(`${server.baseUrl}/timetable/generate`);
  await expect(page.getByText(generation.readinessReady)).toBeVisible();
  await page.getByLabel(generation.timeLimit).selectOption("10");
  await page.getByRole("button", { name: generation.start }).click();
}

test("(M1) lifecycle: compare, undo and redo, archive, restore, keep manual edits when regenerating, and the history", async ({ browser, page }, testInfo) => {
  test.setTimeout(360_000);
  await server.start(browser, "phase5-lifecycle");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Lifecycle-Owner-1");
    const yearId = await seedReadySchool(page, server.baseUrl);
    await generate(page);
    await expect(page.getByRole("heading", { name: generation.resultTitle })).toBeVisible({ timeout: 90_000 });
    await expect(page.getByText(generation.statuses.completed).first()).toBeVisible();

    // A generated version is a draft; nothing is locked and no question is asked the first time.
    await page.getByRole("link", { name: generation.openTimetable }).click();
    await expect(page.getByRole("heading", { name: timetable.title, level: 1 })).toBeVisible();
    await expect(page.getByText(lifecycle.statusHints.draft)).toBeVisible();
    await expect(page.getByRole("button", { name: lifecycle.compare })).toHaveCount(0);
    const [first] = await api<TimetableVersionSummary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`);
    const { swap, data } = await findValidSwap(page, first.id);

    // Manual edit: two lessons swapped; undo and redo with the keyboard (Ctrl+Z, Ctrl+Y) and with the buttons.
    await page.getByRole("button", { name: timetable.edit }).click();
    await swapInEditor(page, data, swap);
    await expect(page.getByText(timetable.changes("٢"))).toBeVisible();
    await expect(page.getByText(timetable.noConflicts)).toBeVisible();
    await page.keyboard.press("Control+Z");
    await expect(page.getByText(timetable.changes("٠"))).toBeVisible();
    await page.keyboard.press("Control+Y");
    await expect(page.getByText(timetable.changes("٢"))).toBeVisible();
    await page.keyboard.press("Control+Z");
    await page.keyboard.press("Control+Shift+Z");
    await expect(page.getByText(timetable.changes("٢"))).toBeVisible();
    await page.getByRole("button", { name: timetable.undo }).click();
    await expect(page.getByText(timetable.changes("٠"))).toBeVisible();
    await page.getByRole("button", { name: timetable.redo }).click();
    await expect(page.getByText(timetable.changes("٢"))).toBeVisible();
    await expect(page.getByText(timetable.noConflicts)).toBeVisible();
    await page.getByRole("button", { name: timetable.saveEdit }).click();
    await expect(page.getByText(timetable.savedEdit)).toBeVisible();
    await expect(page.locator(".timetable-versions tbody tr")).toHaveCount(2);
    await expect(page.locator(".timetable-versions tbody tr").first()).toContainText(timetable.sources.edited);

    // Compare with the first version: two moved lessons, marked on the grid with an icon and a text, and axe stays clean.
    await page.getByRole("button", { name: lifecycle.compare }).click();
    await expect(page.getByRole("heading", { name: lifecycle.compareTitle })).toBeVisible();
    await expect(page.getByText(lifecycle.kindCount(lifecycle.kinds.moved, "٢"), { exact: true })).toBeVisible();
    await expect(page.getByText(lifecycle.kindCount(lifecycle.kinds.added, "٠"), { exact: true })).toBeVisible();
    await expect(page.locator(".version-comparison .ui-tt-cell.is-moved")).toHaveCount(2);
    await expect(page.locator(".version-comparison .ui-tt-cell.is-moved svg").first()).toBeVisible();
    await expect(page.getByRole("group", { name: new RegExp(lifecycle.cellMoved("")) }).first()).toBeVisible();
    await expect(page.locator(".comparison-changes li")).toHaveCount(2);
    await page.screenshot({ path: testInfo.outputPath("m1-compare.png"), fullPage: true });
    await expectNoSeriousA11yViolations(page, "version comparison");
    for (const width of [375, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `comparison at ${width}px`);
    }
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.getByRole("button", { name: lifecycle.compareClose }).click();
    await expect(page.getByRole("heading", { name: lifecycle.compareTitle })).toHaveCount(0);

    // Approve the edited version; archive the first one: archived versions can no longer be approved.
    await page.getByRole("button", { name: timetable.approve }).click();
    await page.getByRole("dialog").getByRole("button", { name: timetable.approve }).click();
    await expect(page.getByText(timetable.approvedDone)).toBeVisible();
    await expect(page.getByText(lifecycle.statusHints.approved)).toBeVisible();
    await page.locator(".timetable-versions tbody tr").nth(1).click();
    await page.getByRole("button", { name: lifecycle.archive }).click();
    await expect(page.getByRole("dialog")).toContainText(lifecycle.archiveConfirm("١"));
    await page.getByRole("dialog").getByRole("button", { name: lifecycle.archive }).click();
    await expect(page.getByText(lifecycle.archivedDone)).toBeVisible();
    await expect(page.getByText(lifecycle.statusHints.archived)).toBeVisible();
    await expect(page.getByRole("button", { name: timetable.approve })).toHaveCount(0);
    await expect(page.getByRole("button", { name: lifecycle.archive })).toHaveCount(0);

    // Restore the archived version: a NEW draft (version 3); the approved version and the archived one stay as they are.
    await page.getByRole("button", { name: lifecycle.rollback }).click();
    await expect(page.getByRole("dialog")).toContainText(lifecycle.rollbackConfirm("١"));
    await page.getByRole("dialog").getByRole("button", { name: lifecycle.rollback }).click();
    await expect(page.getByText(lifecycle.rollbackDone("٣"))).toBeVisible();
    const rows = page.locator(".timetable-versions tbody tr");
    await expect(rows).toHaveCount(3);
    await expect(rows.nth(0)).toContainText(lifecycle.sourceRolledBack);
    await expect(rows.nth(0)).toContainText(lifecycle.statuses.draft);
    await expect(rows.nth(1)).toContainText(lifecycle.statuses.approved);
    await expect(rows.nth(2)).toContainText(lifecycle.statuses.archived);
    await page.screenshot({ path: testInfo.outputPath("m1-versions.png"), fullPage: true });
    await expectNoSeriousA11yViolations(page, "timetable versions with the lifecycle actions");

    // The latest version is not a manual edit: regenerating asks nothing.
    await generate(page);
    await expect(page.getByRole("dialog")).toHaveCount(0);
    await expect(page.getByText(lifecycle.keepEditsKeep)).toHaveCount(0);
    const versionCount = async () => (await api<TimetableVersionSummary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`)).length;
    await expect.poll(versionCount, { timeout: 90_000 }).toBe(4);

    // Edit again (the latest version is now a manual edit), then regenerate over it: the question appears.
    await page.goto(`${server.baseUrl}/timetable/view`);
    const latest = (await api<TimetableVersionSummary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`))[0];
    const second = await findValidSwap(page, latest.id);
    await page.goto(`${server.baseUrl}/timetable/view?version=${latest.id}`);
    await page.getByRole("button", { name: timetable.edit }).click();
    await swapInEditor(page, second.data, second.swap);
    await page.getByRole("button", { name: timetable.saveEdit }).click();
    await expect(page.getByText(timetable.savedEdit)).toBeVisible();

    await generate(page);
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(lifecycle.keepEditsTitle);
    await expect(dialog.getByRole("radio", { name: new RegExp(lifecycle.keepEditsKeep) })).toBeChecked();
    await page.screenshot({ path: testInfo.outputPath("m1-keep-dialog.png") });
    await expectNoSeriousA11yViolations(page, "keep manual edits dialog");
    // Cancelling starts nothing.
    await dialog.getByRole("button", { name: messages.app.cancel }).click();
    await expect(dialog).toBeHidden();
    await expect(page.getByRole("heading", { name: generation.progressTitle })).toHaveCount(0);
    await page.getByRole("button", { name: generation.start }).click();
    await page.getByRole("dialog").getByRole("button", { name: lifecycle.keepEditsConfirm }).click();
    await expect(page.getByText(lifecycle.locksKept("حصتان"))).toBeVisible({ timeout: 90_000 });

    // The regenerated version still has the two lessons where the owner put them.
    const newest = (await api<TimetableVersionSummary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`))[0];
    const kept = await api<Timetable>(page, server.baseUrl, "GET", `/timetables/${newest.id}`);
    expect(kept.violations).toBe(0);
    const placed = (lineId: number, day: number, lesson: number) => kept.lessons.some((item) => item.lineId === lineId && item.day === day && item.lesson === lesson);
    expect(placed(second.swap.first.lineId, second.swap.second.day, second.swap.second.lesson)).toBe(true);
    expect(placed(second.swap.second.lineId, second.swap.first.day, second.swap.first.lesson)).toBe(true);

    // History: newest first, Arabic sentences from codes, filter by type, axe clean.
    await page.goto(`${server.baseUrl}/settings/history`);
    await expect(page.getByRole("heading", { name: audit.title, level: 1 })).toBeVisible();
    const history = page.locator(".ui-table tbody tr");
    await expect(history.first()).toContainText(audit.events.GenerationFinished({ status: audit.paramLabels.status.completed }));
    await expect(page.locator(".ui-table")).toContainText(audit.events.TimetableApproved({ number: "٢" }));
    await expect(page.locator(".ui-table")).toContainText(audit.events.TimetableRolledBack({ number: "٣", from: "١" }));
    await expect(page.locator("main")).not.toContainText(/[A-Za-z]/);
    await page.getByLabel(audit.categoryLabel).selectOption("timetable");
    await expect(page.locator(".ui-table tbody tr").first()).not.toContainText(audit.categories.generation);
    await expect(page.locator(".ui-table tbody tr", { hasText: audit.categories.generation })).toHaveCount(0);
    await expect(page.locator(".ui-table")).toContainText(audit.events.TimetableArchived({ number: "١" }));
    await page.screenshot({ path: testInfo.outputPath("m1-history.png"), fullPage: true });
    await expectNoSeriousA11yViolations(page, "history");
    for (const width of [375, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `history at ${width}px`);
    }
  } finally {
    await server.stop();
  }
});
