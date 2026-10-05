import { expect, test, type Browser, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { formatNumber } from "../src/lib/format";
import { prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { breakpoints, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, goToSection, setupOwner } from "./support/flows";

// The owner's suggested Iraqi curriculum (ADR 0028–0030), scenarios (a)–(g) of the instruction, each on a fresh
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
  await expect(page.getByRole("status").filter({ hasText: /تمت تعبئة المنهج المقترح/ })).toBeVisible();
}

test("(a) primary: six grades filled, then the daily suggestion makes every stage match", async ({ browser, page }) => {
  const server = await school_(browser, page, "curriculum-a", { schoolType: "primary", shiftMode: "morning", grades: primary });
  try {
    await openPanel(page);
    await expectNoSeriousA11yViolations(page, "suggested curriculum panel");
    await applySuggested(page);
    const expected = [28, 28, 27, 29, 30, 30];
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

test("(b) intermediate without French 30/30/31, with French 33/33/34; (e) a second apply changes nothing; (f) an edited value stays", async ({ browser, page }) => {
  const server = await school_(browser, page, "curriculum-b", { schoolType: "intermediate", shiftMode: "morning", grades: intermediate });
  try {
    await openPanel(page);
    const french = panel(page).getByRole("checkbox", { name: "اللغة الفرنسية" });
    await expect(french).not.toBeChecked(); // optional subjects start unchecked
    await expect(panel(page).locator(".suggested-stage-line")).toContainText([/٣٠ حصة$/, /٣٠ حصة$/, /٣١ حصة$/]);
    await applySuggested(page);
    for (const [index, total] of [30, 30, 31].entries()) await expect(totals(page).nth(index)).toContainText(curriculum.plannedOf(arab(total), arab(35)));

    // (e) Applying again would change nothing.
    await expect(panel(page).getByText(suggested.nothing)).toBeVisible();

    // French checked: 33/33/34 after applying (only the French lines are added).
    await french.check();
    await expect(panel(page).locator(".suggested-stage-line")).toContainText([/٣٣ حصة$/, /٣٣ حصة$/, /٣٤ حصة$/]);
    await applySuggested(page);
    for (const [index, total] of [33, 33, 34].entries()) await expect(totals(page).nth(index)).toContainText(curriculum.plannedOf(arab(total), arab(35)));

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

test("(c) preparatory with both branches: the literary stages carry the review warning", async ({ browser, page }) => {
  const server = await school_(browser, page, "curriculum-c", { schoolType: "preparatory", shiftMode: "morning", grades: preparatory() });
  try {
    await openPanel(page);
    await expect(panel(page).locator(".suggested-stage")).toHaveCount(6);
    for (const stage of ["الرابع الأدبي", "الخامس الأدبي", "السادس الأدبي"])
      await expect(panel(page).locator(".suggested-stage", { hasText: stage }).getByText(suggested.review)).toBeVisible();
    await expect(panel(page).locator(".suggested-stage", { hasText: "الرابع العلمي" }).locator(".review-warning")).toHaveCount(0);
    await applySuggested(page);
    await expect(page.locator(".curriculum-table thead .review-warning")).toHaveCount(3);
    await expectNoSeriousA11yViolations(page, "review warnings");
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
    // Evening stages: 6 lessons × 5 days = 30; السادس العلمي (33) does not fit and says why.
    const dailyCard = page.locator(".page-card", { hasText: daily.title });
    const sixthScientific = dailyCard.getByRole("row", { name: /السادس العلمي/ });
    await expect(sixthScientific).toContainText(daily.statuses.aboveCapacity);
    await expect(sixthScientific).toContainText(daily.aboveHint("٣٣ حصة", "٣٠ حصة"));
    await dailyCard.getByRole("button", { name: daily.apply }).click();
    await page.getByRole("dialog", { name: daily.confirmTitle }).getByRole("button", { name: daily.apply }).click();
    await expect(page.getByRole("status").filter({ hasText: daily.applied })).toBeVisible();
    await expect(totals(page).filter({ hasText: curriculum.status.equal })).toHaveCount(8);

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
