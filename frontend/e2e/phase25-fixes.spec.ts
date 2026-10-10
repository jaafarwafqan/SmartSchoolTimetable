import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { arabicCount } from "../src/lib/arabicCount";
import { formatNumber } from "../src/lib/format";
import { prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { breakpoints, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, goToSection, setupOwner } from "./support/flows";

// Regression tests for the owner's findings B1–B8 (Phase 2.5 fixes). A secondary school with both preparatory
// branches gives nine stage columns, the case that broke the curriculum table.
const server = new ApiServer();
const school = messages.school;
const wizard = school.wizard;
const curriculum = school.curriculum;
const templates = school.templates;
const arab = (value: number) => formatNumber(value, "arab");
const stepOrder = ["المدرسة", "السنة الدراسية", "الدوام", "الصفوف والشعب", "المواد والمنهج", "المعلمون", "الأنصبة", "المراجعة"];

test.describe.configure({ mode: "serial" });
test.beforeAll(async ({ browser }) => { await server.start(browser, "phase25-fixes"); });
test.afterAll(async () => { await server.stop(); });

async function atEveryWidth(page: Page, screen: string, check?: () => Promise<void>) {
  for (const width of breakpoints) {
    await page.setViewportSize({ width, height: 900 });
    await expectNoPageScrollX(page, `${screen}`);
    if (check) await check();
  }
  await page.setViewportSize({ width: 1280, height: 900 });
}

test("curriculum table, wizard layout, subject chips, counts and month names", async ({ page }) => {
  await setupOwner(page, server.baseUrl, "owner", "Fixes-Owner-1");
  const { yearId } = await prepareSchool(page, server.baseUrl, {
    schoolType: "secondary", shiftMode: "morning",
    grades: ["intermediate-1", "intermediate-2", "intermediate-3"].map((gradeKey) => ({ gradeKey, sections: 2 }))
      .concat(["preparatory-4", "preparatory-5", "preparatory-6"].map((gradeKey) => ({ gradeKey, branches: ["scientific", "literary"], sections: 1 }))),
    subjects: ["الرياضيات"],
  });
  expect(yearId).toBeGreaterThan(0);

  // B5: the step list always reads in the same order, and each step's title matches its place.
  await page.goto(`${server.baseUrl}/setup`);
  const steps = page.getByRole("navigation", { name: wizard.progressLabel }).locator(".wizard-step-label");
  await expect(steps).toHaveText(stepOrder);
  expect(Object.values(wizard.steps)).toEqual(stepOrder);
  await expect(page.getByRole("heading", { level: 2, exact: true, name: wizard.steps[4] })).toBeVisible(); // resumed after steps 1–3
  for (const step of [3, 2, 1] as const) {
    await page.getByRole("button", { name: wizard.back }).click();
    await expect(page.getByRole("heading", { level: 2, exact: true, name: wizard.steps[step] })).toBeVisible();
    await expect(page.locator("[aria-current=step]")).toContainText(wizard.steps[step]);
    await atEveryWidth(page, `wizard step ${step}`);
    if (step === 3) await expectNoTextOverlap(page.locator(".shift-system-block").first(), "periods preview");
    if (step === 2) await expect(page.getByText(/أيلول/).first()).toBeVisible(); // B8: Iraqi month name in the date hint
  }
  for (const step of [2, 3, 4] as const) {
    await page.getByRole("button", { name: wizard.next }).click();
    await expect(page.getByRole("heading", { level: 2, exact: true, name: wizard.steps[step] })).toBeVisible();
  }
  await atEveryWidth(page, "wizard step 4", () => expectNoTextOverlap(page.locator(".stage-cards"), "stage cards"));
  await page.getByRole("button", { name: wizard.next }).click();
  await expect(page.getByRole("heading", { level: 2, exact: true, name: wizard.steps[5] })).toBeVisible();

  // B6: a subject typed by name appears at once as a chip marked «مضافة» and as a row of the table; removing it can be undone.
  const chips = page.locator(".subject-chips");
  await expect(chips.locator(".subject-chip")).toHaveCount(1);
  await page.getByLabel(school.subjects.newName).fill("الكيمياء");
  await page.getByRole("button", { name: wizard.curriculum.quickAdd }).click();
  const chemistry = chips.locator(".subject-chip", { hasText: "الكيمياء" });
  await expect(chemistry).toContainText(templates.addedBadge);
  await expect(page.getByRole("rowheader", { name: /الكيمياء/ })).toBeVisible();
  await chemistry.getByRole("button", { name: templates.removeSubject("الكيمياء") }).click();
  await expect(chemistry).toHaveCount(0);
  await expect(page.getByRole("rowheader", { name: /الكيمياء/ })).toHaveCount(0);
  await page.getByRole("button", { name: templates.undo }).click();
  await expect(chips.locator(".subject-chip", { hasText: "الكيمياء" })).toBeVisible();

  // B7: the counted message agrees with the number of subjects added. «الكيمياء» is also a suggestion: wait until the
  // restored subject has left the suggestions before counting them.
  await expect(page.locator(".subject-chips ~ fieldset").getByText("الكيمياء", { exact: true })).toHaveCount(0);
  const suggestionCount = await page.locator(".subject-chips ~ fieldset input[type=checkbox]").count();
  expect(suggestionCount).toBeGreaterThan(2);
  await page.getByRole("button", { name: templates.preview }).first().click();
  await page.getByRole("button", { name: templates.apply }).click();
  await expect(page.getByRole("status").filter({ hasText: templates.subjectsApplied(arabicCount(suggestionCount, "subject", arab, "oblique")) })).toBeVisible();
  await expect(chips.locator(".subject-chip")).toHaveCount(2 + suggestionCount);

  // B1/B2/M3: one header and one totals cell per stage, full names with capacity, nothing overlapping or clipped.
  const cell = page.locator(".curriculum-input").first();
  await cell.fill("5");
  await cell.press("Enter");
  await expect(page.getByRole("status").filter({ hasText: curriculum.saved })).toBeVisible();
  const table = page.locator(".curriculum-table");
  const headers = table.locator("thead th.curriculum-stage-head");
  await expect(headers).toHaveCount(9);
  await expect(table.locator("tfoot td")).toHaveCount(9);
  await expect(headers.first()).toContainText("الأول المتوسط");
  await expect(headers.first()).toContainText(curriculum.headerCapacity(arab(35)));
  await expect(table.locator("tfoot td").first()).toContainText(curriculum.plannedOf(arab(5), arab(35)));
  for (let index = 0; index < 9; index++) {
    const head = await headers.nth(index).boundingBox();
    const total = await table.locator("tfoot td").nth(index).boundingBox();
    expect(head && total && Math.abs(head.x - total.x) < 1 && Math.abs(head.width - total.width) < 1, `totals cell ${index} sits under its column`).toBeTruthy();
    const clipped = await headers.nth(index).evaluate((element) => element.scrollWidth > element.clientWidth + 1 || element.scrollHeight > element.clientHeight + 1);
    expect(clipped, `header ${index} is not clipped`).toBe(false);
  }
  const firstInput = await cell.boundingBox();
  const lastHead = await headers.first().boundingBox();
  expect(firstInput && lastHead && firstInput.y >= lastHead.y + lastHead.height - 1, "inputs start below the header").toBeTruthy();
  expect(firstInput && firstInput.width <= 52 && firstInput.height <= 40, "compact inputs").toBeTruthy();
  await expectNoTextOverlap(table, "curriculum table");
  await atEveryWidth(page, "wizard step 5", () => expectNoTextOverlap(page.locator(".curriculum-table thead"), "curriculum header"));
  await expectNoSeriousA11yViolations(page, "wizard step 5 with chips");

  for (const step of [6, 7] as const) {
    await page.getByRole("button", { name: step === 6 ? wizard.next : wizard.skip }).click();
    if (step === 6) await expect(page.getByRole("heading", { level: 2, exact: true, name: wizard.steps[6] })).toBeVisible();
  }
  await atEveryWidth(page, "wizard step 7");

  // Main screens: no horizontal page scroll at any width; no overlap on the dashboard and the curriculum tab.
  await page.goto(`${server.baseUrl}/`);
  await atEveryWidth(page, "dashboard", () => expectNoTextOverlap(page.locator(".dashboard-grid"), "dashboard"));
  for (const label of [school.nav.profile, school.nav.academicYears, school.nav.scheduleStructure, school.nav.calendar, school.nav.stagesSections, school.nav.subjects, school.nav.curriculum, school.nav.teachers, school.nav.settings]) {
    await goToSection(page, label);
    await atEveryWidth(page, label);
  }
  await expectNoTextOverlap(page.locator(".settings-grid"), "settings");
  await goToSection(page, school.nav.academicYears);
  await expect(page.getByText(/أيلول/).first()).toBeVisible(); // B8 on a list screen
  await expect(page.getByText(/سبتمبر|يونيو/)).toHaveCount(0);
});

test("the overlap guard catches stacked totals like the old layout", async ({ page }) => {
  // The pre-fix markup: totals cells laid out as grids inside a table cell stack into one column and overlap.
  await page.setContent(`<html dir="rtl"><body><table><tfoot><tr>
    <td style="display:grid"><strong>مجموع الحصص: ٥</strong><span style="margin-block-start:-1.2em">الدوام الصباحي: السعة ٣٥</span></td>
  </tr></tfoot></table><p>نص منفصل</p></body></html>`);
  await expect(expectNoTextOverlap(page.locator("table"), "old totals")).rejects.toThrow(/overlapping texts/);
  await expectNoTextOverlap(page.locator("p"), "single text");
});
