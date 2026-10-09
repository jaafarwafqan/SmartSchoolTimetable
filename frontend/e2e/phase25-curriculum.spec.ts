import { expect, test, type Browser, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { formatNumber } from "../src/lib/format";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { breakpoints, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, goToSection, setupOwner } from "./support/flows";

// The official Iraqi study plan 2026-2027 (ADR 0028–0030), scenarios (a)–(g) of the instruction, each on a fresh
// temporary database.
const school = messages.school;
const suggested = school.suggested;
const daily = school.daily;
const curriculum = school.curriculum;
const arab = (value: number) => formatNumber(value, "arab");
const primary = ["primary-1", "primary-2", "primary-3", "primary-4", "primary-5", "primary-6"].map((gradeKey) => ({ gradeKey, sections: 1 }));
const intermediate = ["intermediate-1", "intermediate-2", "intermediate-3"].map((gradeKey) => ({ gradeKey, sections: 1 }));
const preparatory = (shift: "morning" | "evening" = "morning") =>
  ["preparatory-4", "preparatory-5", "preparatory-6"].map((gradeKey) => ({ gradeKey, branches: ["scientific", "literary"], sections: 1, shift }));

test.describe.configure({ mode: "serial" });

async function school_(browser: Browser, page: Page, prefix: string, options: Parameters<typeof prepareSchool>[2]) {
  const server = new ApiServer();
  await server.start(browser, prefix);
  await setupOwner(page, server.baseUrl, "owner", "Curriculum-Owner-1");
  await prepareSchool(page, server.baseUrl, options);
  await goToSection(page, school.nav.curriculum);
  return server;
}

const panel = (page: Page) => page.locator(".suggested-panel");
const totals = (page: Page) => page.locator(".curriculum-table tfoot td");

async function openPanel(page: Page) {
  await panel(page).getByText(suggested.open, { exact: true }).first().click();
  await expect(panel(page).getByText(suggested.provenance)).toBeVisible();
}

async function applySuggested(page: Page) {
  await panel(page).getByRole("button", { name: suggested.apply }).click();
  await expect(page.getByRole("status").filter({ hasText: /تمت تعبئة المنهج من الخطة الرسمية/ })).toBeVisible();
}

test("(a) primary: six grades filled, then the daily suggestion makes every stage match", async ({ browser, page }) => {
  const server = await school_(browser, page, "curriculum-a", { schoolType: "primary", shiftMode: "morning", grades: primary });
  try {
    await openPanel(page);
    await expectNoSeriousA11yViolations(page, "suggested curriculum panel");
    // الرابع الابتدائي: 30 as printed (owner decision #66); no stage carries a review warning.
    const fourth = panel(page).locator(".suggested-stage", { hasText: "الرابع الابتدائي" });
    await expect(fourth).toContainText(suggested.officialTotal("٣٠ حصة"));
    await expect(fourth).toContainText(suggested.enabledTotal("٣٠ حصة"));
    await expect(panel(page).locator(".review-warning")).toHaveCount(0);
    await applySuggested(page);
    const expected = [30, 30, 30, 30, 30, 31];
    for (const [index, total] of expected.entries()) await expect(totals(page).nth(index)).toContainText(curriculum.plannedOf(arab(total), arab(35)));
    await expect(page.locator(".suggested-mark").first()).toHaveText(suggested.badge);

    const dailyCard = page.locator(".page-card", { hasText: daily.title });
    await expect(dailyCard.getByRole("checkbox")).toHaveCount(6); // every stage has a new suggestion, all selected
    await dailyCard.getByRole("button", { name: daily.apply }).click();
    await page.getByRole("dialog", { name: daily.confirmTitle }).getByRole("button", { name: daily.apply }).click();
    await expect(page.getByRole("status").filter({ hasText: daily.applied })).toBeVisible();
    for (const [index, total] of expected.entries()) await expect(totals(page).nth(index)).toContainText(curriculum.plannedOf(arab(total), arab(total)));
    await expect(totals(page).filter({ hasText: curriculum.status.equal })).toHaveCount(6);
    await expect(dailyCard.getByText(daily.statuses.same)).toHaveCount(6);
    await expectNoSeriousA11yViolations(page, "daily suggestion applied");
  } finally {
    await server.stop();
  }
});

test("(b) intermediate 30/30/30, then French and computing ticked 34/34/32; (e) a second apply changes nothing; (f) an edited value stays", async ({ browser, page }) => {
  const server = await school_(browser, page, "curriculum-b", { schoolType: "intermediate", shiftMode: "morning", grades: intermediate });
  try {
    await openPanel(page);
    const french = panel(page).getByRole("checkbox", { name: "اللغة الفرنسية" });
    const computing = panel(page).getByRole("checkbox", { name: "الحاسوب" });
    await expect(french).not.toBeChecked(); // optional subjects start unchecked
    await expect(computing).not.toBeChecked();
    await expect(panel(page).locator(".suggested-stage-line")).toContainText([/٣٠ حصة$/, /٣٠ حصة$/, /٣٠ حصة$/]);
    await applySuggested(page);
    for (const [index, total] of [30, 30, 30].entries()) await expect(totals(page).nth(index)).toContainText(curriculum.plannedOf(arab(total), arab(35)));
    await expect(page.locator(".curriculum-table tbody", { hasText: "اللغة الفرنسية" })).toHaveCount(0); // unticked: never created

    // (e) Applying again would change nothing.
    await expect(panel(page).getByText(suggested.nothing)).toBeVisible();

    // French and computing ticked: added on top of the official 30 (computing is not taught in the third grade).
    await french.check();
    await computing.check();
    await expect(panel(page).locator(".suggested-stage-line")).toContainText([/٣٤ حصة$/, /٣٤ حصة$/, /٣٢ حصة$/]);
    const first = panel(page).locator(".suggested-stage").first();
    await expect(first).toContainText(suggested.officialTotal("٣٠ حصة"));
    await expect(first).toContainText(suggested.enabledTotal("٣٤ حصة"));
    await expect(first.getByText(suggested.totalDiffers)).toBeVisible();
    await applySuggested(page);
    for (const [index, total] of [34, 34, 32].entries()) await expect(totals(page).nth(index)).toContainText(curriculum.plannedOf(arab(total), arab(35)));
    for (const subject of ["اللغة الفرنسية", "الحاسوب"]) await expect(page.locator(".curriculum-table tbody").getByText(subject, { exact: true })).toHaveCount(1);
    await expectNoSeriousA11yViolations(page, "official plan with optional subjects");

    // (f) An edited suggested value loses «مقترح» and is kept by later runs; the stage reset restores it after confirming.
    const cell = page.locator(".curriculum-input").first();
    const original = await cell.inputValue();
    await cell.fill("1");
    await cell.press("Enter");
    await expect(page.getByRole("status").filter({ hasText: curriculum.saved })).toBeVisible();
    await expect(page.locator(".curriculum-cell").first().locator(".suggested-mark")).toHaveCount(0);
    await expect(panel(page).getByText(suggested.nothing)).toBeVisible();
    await expect(cell).toHaveValue(arab(1));
    await panel(page).getByRole("button", { name: suggested.reset }).first().click();
    const dialog = page.getByRole("dialog", { name: suggested.resetTitle });
    await expect(dialog).toContainText(`${arab(1)} ← ${original}`);
    await dialog.getByRole("button", { name: suggested.resetConfirm }).click();
    await expect(cell).toHaveValue(original);
  } finally {
    await server.stop();
  }
});

test("(c) preparatory with both branches: no review questions, حزب البعث only in the fifth; Kurdish counts in the official total", async ({ browser, page }) => {
  const server = await school_(browser, page, "curriculum-c", { schoolType: "preparatory", shiftMode: "morning", grades: preparatory() });
  try {
    await openPanel(page);
    await expect(panel(page).locator(".suggested-stage")).toHaveCount(6);
    const fourthScientific = panel(page).locator(".suggested-stage", { hasText: "الرابع العلمي" });
    await expect(panel(page).locator(".review-warning")).toHaveCount(0);
    // منهج جرائم حزب البعث (owner decision #67): ticking it adds one lesson to the fifth grades only.
    const fifthScientific = panel(page).locator(".suggested-stage", { hasText: "الخامس العلمي" });
    await panel(page).getByRole("checkbox", { name: "منهج جرائم حزب البعث" }).check();
    await expect(fifthScientific).toContainText(suggested.enabledTotal("٣٠ حصة")); // 29 without Kurdish + 1
    await expect(fourthScientific).toContainText(suggested.enabledTotal("٢٨ حصة"));
    await panel(page).getByRole("checkbox", { name: "منهج جرائم حزب البعث" }).uncheck();
    // Kurdish: optional, unticked → 28 against the official 30; ticked → exactly 30.
    await expect(fourthScientific).toContainText(suggested.enabledTotal("٢٨ حصة"));
    await panel(page).getByRole("checkbox", { name: "اللغة الكردية" }).check();
    await expect(fourthScientific).toContainText(suggested.enabledTotal("٣٠ حصة"));
    await expect(panel(page).locator(".optional-subject", { hasText: "اللغة الكردية" })).toContainText(suggested.optionalCounted);
    await expect(panel(page).locator(".optional-subject", { hasText: "اللغة الفرنسية" })).toContainText(suggested.optionalExtra);
    await applySuggested(page);
    await expect(page.locator(".curriculum-table thead .review-warning")).toHaveCount(0);
    await expectNoSeriousA11yViolations(page, "official plan without review warnings");
  } finally {
    await server.stop();
  }
});

test("(h) a stage without a template is matched from a list; optional subjects say what capacity they need", async ({ browser, page }) => {
  const server = new ApiServer();
  await server.start(browser, "curriculum-h");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Curriculum-Owner-1");
    const { yearId } = await prepareSchool(page, server.baseUrl, { schoolType: "preparatory", shiftMode: "morning", grades: [{ gradeKey: "preparatory-6", branches: ["scientific"], sections: 1 }] });
    await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/stages/`, { name: "صف الموهوبين", displayOrder: 90, version: 0 });
    await goToSection(page, school.nav.curriculum);
    await openPanel(page);

    // Listed under «مراحل لا يوجد لها قالب رسمي»; the official stage is chosen from a list, never typed.
    const unmatched = panel(page).locator(".suggested-unmatched");
    await expect(unmatched.getByRole("heading", { name: suggested.unmatchedTitle })).toBeVisible();
    await expect(panel(page).locator(".suggested-stage", { hasText: "صف الموهوبين" })).toHaveCount(0);
    await unmatched.getByLabel(suggested.unmatchedChoice("صف الموهوبين")).selectOption("الخامس العلمي");
    const gifted = panel(page).locator(".suggested-stage", { hasText: "صف الموهوبين" });
    await expect(gifted).toContainText(suggested.officialTotal("٣٠ حصة"));

    // French and computing: السادس العلمي 33 → 37, 8 lessons a day, above the 7 × 5 = 35 shift; applying is still allowed.
    await panel(page).getByRole("checkbox", { name: "اللغة الفرنسية" }).check();
    await panel(page).getByRole("checkbox", { name: "الحاسوب" }).check();
    const sixth = panel(page).locator(".suggested-stage", { hasText: "السادس العلمي" });
    await expect(sixth.locator(".capacity-note")).toContainText(suggested.capacityAfter("٣٧ حصة", "٨ حصص"));
    await expect(sixth.locator(".capacity-note")).toContainText(suggested.capacityAbove("٣٥ حصة"));
    await expect(gifted.locator(".capacity-note")).toContainText(suggested.capacityAfter("٣٣ حصة", "٧ حصص"));
    await expect(gifted.locator(".capacity-note.is-above")).toHaveCount(0);
    await expectNoSeriousA11yViolations(page, "unmatched stage and capacity notes");
    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `unmatched stage at ${width}px`);
      await expectNoTextOverlap(panel(page), `suggested panel with an unmatched stage at ${width}px`);
    }
    await page.setViewportSize({ width: 1280, height: 900 });
    await applySuggested(page);
    await expect(totals(page)).toHaveCount(2);
    await expect(totals(page).nth(1)).toContainText(arab(33)); // the matched stage, applied (no sections yet)
    await expect(totals(page).nth(1)).toContainText(curriculum.noSections);
  } finally {
    await server.stop();
  }
});

test("(d) a dual-shift ثانوية; (g) no overflow or overlapping text at four widths", async ({ browser, page }) => {
  const server = await school_(browser, page, "curriculum-d", {
    schoolType: "secondary", shiftMode: "dual", grades: [...intermediate, ...preparatory("evening")],
  });
  try {
    await openPanel(page);
    await applySuggested(page);
    await expect(totals(page)).toHaveCount(9);
    // Evening stages: 6 lessons × 5 days = 30; السادس العلمي (33) and السادس الأدبي (31) do not fit and say why.
    const dailyCard = page.locator(".page-card", { hasText: daily.title });
    const sixthScientific = dailyCard.getByRole("row", { name: /السادس العلمي/ });
    await expect(sixthScientific).toContainText(daily.statuses.aboveCapacity);
    await expect(sixthScientific).toContainText(daily.aboveHint("٣٣ حصة", "٣٠ حصة"));
    await dailyCard.getByRole("button", { name: daily.apply }).click();
    await page.getByRole("dialog", { name: daily.confirmTitle }).getByRole("button", { name: daily.apply }).click();
    await expect(page.getByRole("status").filter({ hasText: daily.applied })).toBeVisible();
    await expect(totals(page).filter({ hasText: curriculum.status.equal })).toHaveCount(7);

    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `curriculum with suggestions at ${width}px`);
      await expectNoTextOverlap(page.locator(".suggested-panel"), `suggested panel at ${width}px`);
      await expectNoTextOverlap(dailyCard, `daily suggestion at ${width}px`);
      await expectNoTextOverlap(page.locator(".curriculum-table"), `curriculum table at ${width}px`);
    }
    await page.setViewportSize({ width: 1280, height: 900 });
    await expectNoSeriousA11yViolations(page, "curriculum with suggestions (dual)");
    await goToSection(page, school.nav.stagesSections);
    await expectNoSeriousA11yViolations(page, "stages with the daily suggestion");
    await expectNoPageScrollX(page, "stages with the daily suggestion");
  } finally {
    await server.stop();
  }
});
