import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { moveOrSwap } from "../src/features/timetable/editing";
import type { GridLesson, Timetable, TimetableCheck, TimetableVersionSummary } from "../src/features/timetable/timetableApi";
import { ApiServer } from "./support/apiServer";
import { api } from "./support/api";
import { seedReadySchool } from "./support/readySchool";
import { expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

// Owner (1.0.0 testing): the manual editor works from every view (section, teacher, stage, the whole school) with the same checks, one edit
// history across the views, and the usual save as a new version.
const server = new ApiServer();
const generation = messages.school.generation;
const timetable = messages.school.timetable;
const violations = messages.school.violations.messages;

type Move = { from: GridLesson; to: { day: number; lesson: number } };

/** A move of one lesson within its section that the real check accepts (`valid`) or rejects with a teacher conflict. */
async function findMove(page: Page, data: Timetable, versionId: number, valid: boolean): Promise<Move> {
  for (const from of data.lessons) {
    for (const to of data.lessons.filter((item) => item.sectionId === from.sectionId && item.lineId !== from.lineId)) {
      const edited = moveOrSwap(data.lessons, { sectionId: from.sectionId, day: from.day, lesson: from.lesson }, { sectionId: from.sectionId, day: to.day, lesson: to.lesson });
      const check = await api<TimetableCheck>(page, server.baseUrl, "POST", `/timetables/${versionId}/check`, { lessons: edited, note: null });
      if (valid ? check.violations.length === 0 : check.violations.some((violation) => violation.code === "TEACHER_CONFLICT")) return { from, to: { day: to.day, lesson: to.lesson } };
    }
  }
  throw new Error(valid ? "No valid move in the test school." : "No move that makes a teacher conflict.");
}

async function chooseView(page: Page, view: "section" | "teacher" | "stage" | "master") {
  await page.locator("#timetable-view").selectOption(view);
}

test("(edit views) a lesson is moved from the teacher, stage and school views, conflicts are explained in Arabic, the history spans the views", async ({ browser, page }) => {
  test.setTimeout(360_000);
  await server.start(browser, "phase5-edit-views");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Edit-Views-1");
    const yearId = await seedReadySchool(page, server.baseUrl);
    await page.goto(`${server.baseUrl}/timetable/generate`);
    await expect(page.getByText(generation.readinessReady)).toBeVisible();
    await page.getByLabel(generation.timeLimit).selectOption("10");
    await page.getByRole("button", { name: generation.start }).click();
    await expect(page.getByRole("heading", { name: generation.resultTitle })).toBeVisible({ timeout: 90_000 });
    await page.getByRole("link", { name: generation.openTimetable }).click();
    await expect(page.getByRole("heading", { name: timetable.title, level: 1 })).toBeVisible();
    const [version] = await api<TimetableVersionSummary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`);
    const data = await api<Timetable>(page, server.baseUrl, "GET", `/timetables/${version.id}`);
    const lessonCount = Math.max(...data.sections.flatMap((section) => section.allowedByDay.map((day) => day.lessons)));
    const move = await findMove(page, data, version.id, true);
    const editor = page.locator(".timetable-editor");

    // 1. The teacher view: his lesson moves within its own section (and swaps with what is there).
    await chooseView(page, "teacher");
    await page.locator("#timetable-teacher").selectOption(String(move.from.teacherId));
    await page.getByRole("button", { name: timetable.edit }).click();
    await expect(page.locator("#timetable-view")).toHaveValue("teacher"); // editing no longer forces the section view
    const teacherCell = (day: number, lesson: number) => editor.locator(`.ui-tt-grid [data-cell="${data.days.indexOf(day)}:${lesson - 1}"] .ui-tt-cell`);
    await teacherCell(move.from.day, move.from.lesson).click();
    await expect(editor.locator("p[aria-live=polite]")).toContainText(timetable.selected("").trim().replace(":", ""));
    await teacherCell(move.to.day, move.to.lesson).click();
    await expect(page.getByText(timetable.changes("٢"))).toBeVisible();
    await expect(page.getByText(timetable.noConflicts)).toBeVisible();
    await expectNoSeriousA11yViolations(page, "editing in the teacher view");

    // 2. The section view shows the same edit (one history for every view); undo and redo there.
    await chooseView(page, "section");
    await page.locator("#timetable-section").selectOption(String(move.from.sectionId));
    await expect(page.getByText(timetable.changes("٢"))).toBeVisible();
    await page.getByRole("button", { name: timetable.undo }).click();
    await expect(page.getByText(timetable.changes("٠"))).toBeVisible();

    // 3. The stage view (by days: rows are days × lessons, columns its sections): the same move.
    await chooseView(page, "stage");
    const stageName = data.sections.find((section) => section.id === move.from.sectionId)!.stageName;
    await page.locator("#timetable-stage").selectOption(stageName);
    await expect(page.locator("#timetable-layout")).toHaveValue("byDays");
    const stageSections = data.sections.filter((section) => section.stageName === stageName);
    const byDaysCell = (sections: typeof data.sections, day: number, lesson: number, sectionId: number) =>
      editor.locator(`.ui-tt-grid [data-cell="${data.days.indexOf(day) * lessonCount + lesson - 1}:${sections.findIndex((section) => section.id === sectionId)}"] .ui-tt-cell`);
    await byDaysCell(stageSections, move.from.day, move.from.lesson, move.from.sectionId).click();
    await byDaysCell(stageSections, move.to.day, move.to.lesson, move.from.sectionId).click();
    await expect(page.getByText(timetable.changes("٢"))).toBeVisible();
    await page.getByRole("button", { name: timetable.undo }).click();
    await expect(page.getByText(timetable.changes("٠"))).toBeVisible();

    // 4. The whole school with the days across (the older layout): a move that makes the teacher busy twice is explained in Arabic and
    //    blocks saving; undo, then the valid move, then save as a new version.
    await chooseView(page, "master");
    await page.locator("#timetable-layout").selectOption("bySections");
    const sectionIndex = (id: number) => data.sections.findIndex((section) => section.id === id);
    const bySectionsCell = (sectionId: number, day: number, lesson: number) =>
      editor.locator(`.ui-tt-grid [data-cell="${sectionIndex(sectionId)}:${data.days.indexOf(day) * lessonCount + lesson - 1}"] .ui-tt-cell`);
    const clash = await findMove(page, data, version.id, false);
    await bySectionsCell(clash.from.sectionId, clash.from.day, clash.from.lesson).click();
    await bySectionsCell(clash.from.sectionId, clash.to.day, clash.to.lesson).click();
    await expect(editor.locator(".timetable-conflicts")).toContainText(violations.TEACHER_CONFLICT("").replace(/^: /, "").slice(0, 12));
    await expect(page.getByRole("button", { name: timetable.saveEdit })).toBeDisabled();
    await expect(editor.locator(".ui-tt-cell.is-conflict").first()).toBeVisible();
    await page.keyboard.press("Control+Z");
    await expect(page.getByText(timetable.changes("٠"))).toBeVisible();
    await bySectionsCell(move.from.sectionId, move.from.day, move.from.lesson).click();
    await bySectionsCell(move.from.sectionId, move.to.day, move.to.lesson).click();
    await expect(page.getByText(timetable.noConflicts)).toBeVisible();
    await page.getByRole("button", { name: timetable.saveEdit }).click();
    await expect(page.getByText(timetable.savedEdit)).toBeVisible();
    await expect(page.locator(".timetable-versions tbody tr").first()).toContainText(timetable.sources.edited);
    const saved = await api<Timetable>(page, server.baseUrl, "GET", `/timetables/${(await api<TimetableVersionSummary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`))[0].id}`);
    expect(saved.lessons.some((lesson) => lesson.lineId === move.from.lineId && lesson.sectionId === move.from.sectionId && lesson.day === move.to.day && lesson.lesson === move.to.lesson)).toBe(true);
  } finally {
    await server.stop();
  }
});
