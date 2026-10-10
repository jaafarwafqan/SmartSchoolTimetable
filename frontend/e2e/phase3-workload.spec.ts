import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { formatNumber } from "../src/lib/format";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import {
  breakpoints, expectBreakpointScreenshots, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, goToSection, setupOwner,
} from "./support/flows";

// Phase 3C «الأنصبة» on a fresh temporary database: the matrix (assign, outside specialization and its one-click
// fix, completion), the teacher loads, a previewed bulk action, the shortage warning, teacher protection and
// clearing a curriculum line that has assignments.
const school = messages.school;
const workload = school.workload;
const arab = (value: number) => formatNumber(value, "arab");

type Subject = { id: number; name: string };
type Teacher = { id: number };
type Table = { stages: { id: number }[] };

async function seed(page: Page, baseUrl: string) {
  const { yearId } = await prepareSchool(page, baseUrl, { schoolType: "intermediate", shiftMode: "morning", grades: [{ gradeKey: "intermediate-1", sections: 2 }] });
  const subject = (name: string) => api<Subject>(page, baseUrl, "POST", "/subjects/", {
    name, colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false, requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
  });
  const maths = await subject("الرياضيات");
  const arabic = await subject("اللغة العربية");
  const table = await api<Table>(page, baseUrl, "GET", `/academic-years/${yearId}/curriculum`);
  const stageId = table.stages[0].id;
  for (const [id, lessons] of [[maths.id, 5], [arabic.id, 6]] as const) {
    await api(page, baseUrl, "PUT", `/academic-years/${yearId}/curriculum/cell`, { stageId, subjectId: id, label: null, weeklyLessons: lessons, entryId: null, version: null });
  }
  const teacher = (fullName: string, shortName: string, maxLessonsPerWeek: number | null, specializationIds: number[]) =>
    api<Teacher>(page, baseUrl, "POST", "/teachers/", {
      fullName, shortName, offDays: [], blockedPeriods: [], fullyReleased: false, releaseReason: null, releaseFrom: null, releaseTo: null,
      maxLessonsPerDay: null, maxLessonsPerWeek, notes: null, version: 0, specializationIds,
    });
  await teacher("أحمد علي حسن", "أحمد", 10, [maths.id]);
  await teacher("علي كاظم جواد", "علي", null, [arabic.id]);
  return { yearId };
}

const cell = (page: Page, subject: string, section: string) => page.getByRole("combobox", { name: workload.cellLabel(subject, section) });

test("(3C) workload: matrix, loads, bulk class teacher, shortage, protection and clearing a line with assignments", async ({ browser, page }) => {
  const server = new ApiServer();
  await server.start(browser, "phase3-workload");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Workload-Owner-1");
    await seed(page, server.baseUrl);
    await goToSection(page, school.nav.workload);
    await expect(page.locator(".workload-flag", { hasText: workload.unassigned })).toHaveCount(4);

    // Assign mathematics in أ to Ahmed (a specialist), then Arabic in أ to Ahmed with «عرض الجميع» (outside).
    await cell(page, "الرياضيات", "أ").selectOption({ label: workload.teacherOption("أحمد", arab(0), arab(10)) });
    await expect(page.getByRole("status").filter({ hasText: workload.saved })).toBeVisible();
    await page.getByRole("checkbox", { name: workload.showAll }).check();
    await cell(page, "اللغة العربية", "أ").selectOption({ label: workload.teacherOption("أحمد", arab(5), arab(10)) });
    await expect(page.getByText(workload.outside)).toBeVisible();
    await expect(page.getByText(workload.completion(arab(2), arab(2)))).toBeVisible();

    // Ahmed is now over his limit (11 of 10): the quick warning says so.
    await expect(page.getByText(workload.shortage("أحمد علي حسن", arab(11), arab(10), arab(1)))).toBeVisible();
    // The one-click fix adds Arabic to Ahmed's specializations.
    await page.getByRole("button", { name: workload.addSpecialization }).click();
    await expect(page.getByText(workload.outside)).toHaveCount(0);
    await expectNoSeriousA11yViolations(page, "workload matrix");
    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `workload matrix at ${width}px`);
      await expectNoTextOverlap(page.locator("main"), `workload matrix at ${width}px`);
      const matrixWidth = await page.locator(".workload-matrix").evaluate((element) => element.getBoundingClientRect().width);
      const tableWidth = await page.locator(".workload-matrix > .ui-table-container").evaluate((element) => element.getBoundingClientRect().width);
      expect(tableWidth).toBeGreaterThanOrEqual(matrixWidth - 1);
    }
    await page.setViewportSize({ width: 1280, height: 900 });
    await expectBreakpointScreenshots(page, "phase3-workload-matrix");

    // Bulk: Ali becomes the class teacher of ب (both lines), previewed first.
    await page.getByText(workload.bulk.classTeacher, { exact: true }).click();
    const tool = page.locator("details", { hasText: workload.bulk.classTeacher });
    await tool.getByLabel(workload.bulk.teacher, { exact: true }).selectOption({ label: "علي كاظم جواد" });
    await tool.getByLabel(workload.stage, { exact: true }).selectOption({ label: "الأول المتوسط" });
    await tool.getByLabel(workload.bulk.sectionChoice, { exact: true }).selectOption({ label: "ب" });
    await tool.getByRole("button", { name: workload.bulk.preview }).click();
    await expect(tool.getByText(workload.bulk.actions.create)).toHaveCount(2);
    await tool.getByRole("button", { name: workload.bulk.apply }).click();
    await page.getByRole("dialog", { name: workload.bulk.confirmTitle }).getByRole("button", { name: workload.bulk.apply }).click();
    await expect(tool.getByRole("status")).toBeVisible();
    await expect(page.locator(".workload-flag", { hasText: workload.unassigned })).toHaveCount(0);

    // By teacher: load bars with status.
    await page.getByRole("radio", { name: new RegExp(workload.views.byTeacher) }).check();
    await expect(page.getByRole("meter", { name: workload.loadBar("علي كاظم جواد", arab(11), arab(35)) })).toBeVisible();
    await expect(page.locator(".teacher-load", { hasText: "أحمد علي حسن" }).getByText(workload.status.over)).toBeVisible();
    await expectNoSeriousA11yViolations(page, "teacher loads");
    await expectBreakpointScreenshots(page, "phase3-workload-teachers");

    // Protection: deleting Ali is blocked and the dialog names his assignments.
    await goToSection(page, school.nav.teachers);
    await page.getByRole("button", { name: new RegExp(`^${school.common.delete}.*علي كاظم جواد`) }).click();
    const dialog = page.getByRole("dialog", { name: school.teachers.deleteTitle });
    await expect(dialog.getByText(/نصابان/)).toBeVisible();
    await expect(dialog.getByRole("button", { name: school.common.delete })).toBeDisabled();
    await dialog.getByRole("button", { name: messages.app.cancel }).click();

    // Clearing the Arabic line (assigned in both sections) asks first, then archives the assignments with it.
    await goToSection(page, school.nav.curriculum);
    const arabicCell = page.getByRole("textbox", { name: school.curriculum.cell("اللغة العربية", "الأول المتوسط") });
    await arabicCell.fill("");
    await arabicCell.press("Enter");
    const cascade = page.getByRole("dialog", { name: workload.cascadeTitle });
    await expect(cascade.getByText(/نصابان/)).toBeVisible();
    await cascade.getByRole("button", { name: workload.cascadeConfirm }).click();
    await expect(page.getByText(school.curriculum.cellCleared("اللغة العربية", "الأول المتوسط"))).toBeVisible();
    await page.getByRole("button", { name: school.curriculum.undoClear }).click();
    await expect(page.getByText(school.curriculum.clearUndone)).toBeVisible();
    await goToSection(page, school.nav.workload);
    await expect(page.locator(".workload-flag", { hasText: workload.unassigned })).toHaveCount(0);
  } finally {
    await server.stop();
  }
});
