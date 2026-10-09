import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { breakpoints, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, setupOwner } from "./support/flows";

const server = new ApiServer();
const generation = messages.school.generation;
const timetable = messages.school.timetable;

type Subject = { id: number };
type Curriculum = { stages: Array<{ id: number }> };

/**
 * A small school entered through the normal endpoints (no demo data): one stage, two sections, four subjects
 * (28 lessons a week), five specialised teachers, and the assignment suggester to fill the workload.
 */
async function seedReadySchool(page: Page) {
  const { yearId } = await prepareSchool(page, server.baseUrl, {
    schoolType: "intermediate", shiftMode: "morning", grades: [{ gradeKey: "intermediate-1", sections: 2 }],
  });
  const plan: Array<[string, number, number]> = [["الرياضيات", 8, 1], ["اللغة العربية", 8, 2], ["العلوم", 6, 3], ["التربية الفنية", 6, 4]];
  const subjects: Array<{ id: number; lessons: number }> = [];
  for (const [name, lessons, colorIndex] of plan) {
    const subject = await api<Subject>(page, server.baseUrl, "POST", "/subjects/", {
      name, colorIndex, priority: 0, distributionEnabled: true, spreadAcrossDays: true, heavy: name === "الرياضيات",
      requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
    });
    subjects.push({ id: subject.id, lessons });
  }
  const curriculum = await api<Curriculum>(page, server.baseUrl, "GET", `/academic-years/${yearId}/curriculum`);
  for (const subject of subjects) {
    await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/curriculum/cell`, {
      stageId: curriculum.stages[0].id, subjectId: subject.id, label: null, weeklyLessons: subject.lessons, entryId: null, version: null,
    });
  }
  const names = ["أحمد علي حسن", "سعد كاظم حسن", "زينب جاسم محمد", "مريم عادل كريم", "حسين رعد سالم"];
  for (const [index, fullName] of names.entries()) {
    await api(page, server.baseUrl, "POST", "/teachers/", {
      fullName, shortName: fullName.split(" ")[0], offDays: [], blockedPeriods: [], fullyReleased: false,
      releaseReason: null, releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null,
      notes: null, version: 0, specializationIds: [subjects[index % subjects.length].id],
    });
  }
  await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/workload/suggestions/apply`, { confirm: true });
  return yearId;
}

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
    await expect(page.getByText(generation.verified)).toBeVisible();
    await expect(page.getByRole("table", { name: generation.scoreTitle })).toBeVisible();
    await expect(page.locator("main")).not.toContainText("%");
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

    await page.getByLabel(timetable.viewsLabel).selectOption("teacher");
    await expect(page.getByLabel(timetable.chooseTeacher)).toBeVisible();
    await expect(page.locator(".ui-tt-grid").first()).toBeVisible();
    await page.getByLabel(timetable.viewsLabel).selectOption("master");
    await expect(page.getByRole("table", { name: timetable.views.master })).toBeVisible();
    await expectNoSeriousA11yViolations(page, "timetable viewer");
    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `timetable viewer at ${width}px`);
    }
    await page.setViewportSize({ width: 1280, height: 900 });

    await page.getByRole("button", { name: timetable.approve }).click();
    await page.getByRole("dialog").getByRole("button", { name: timetable.approve }).click();
    await expect(page.getByText(timetable.approvedDone)).toBeVisible();
    await expect(page.getByText(timetable.approved).first()).toBeVisible();

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
