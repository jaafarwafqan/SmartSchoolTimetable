import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { multiStageSchoolPdfFolder } from "./support/pdfOutput";
import { seedMultiStageSchool } from "./support/multiStageSchool";
import { setupOwner } from "./support/flows";

const server = new ApiServer();
const generation = messages.school.generation;
const printing = messages.school.printing;

/** Pages in a PDF Chromium wrote (each page object is «/Type /Page», the tree root is «/Type /Pages»). */
const pdfPages = (pdf: Buffer) => (pdf.toString("latin1").match(/\/Type\s*\/Page(?!s)/g) ?? []).length;

const pt = (value: number) => (value * 96) / 72; // CSS pixels in one point times

async function openPrintPanel(page: Page) {
  await expect(async () => {
    if (!(await page.locator("#print-scope").isVisible())) await page.getByText(printing.title).click();
    await expect(page.locator("#print-scope")).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 30_000 });
}

/** Smallest computed font size (CSS px) of the matching elements while the page is in print media. */
async function smallestFont(page: Page, selector: string): Promise<number> {
  return page.locator(selector).evaluateAll((elements) => Math.min(...elements.map((element) => parseFloat(getComputedStyle(element).fontSize))));
}

test("(M2 printing) colour and black-and-white, stage rows, readable sizes, split pages, a page per stage", async ({ browser, page }, testInfo) => {
  test.setTimeout(300_000);
  await server.start(browser, "m2-printing");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Printing-Owner-2");
    await seedMultiStageSchool(page, server.baseUrl);
    await page.reload();
    await page.goto(`${server.baseUrl}/timetable/generate`);
    await expect(page.getByText(generation.readinessReady)).toBeVisible();
    await page.getByLabel(generation.timeLimit).selectOption("10");
    await page.getByRole("button", { name: generation.start }).click();
    await expect(page.getByRole("heading", { name: generation.resultTitle })).toBeVisible({ timeout: 120_000 });
    await page.getByRole("link", { name: generation.openTimetable }).click();
    await expect(page.locator(".timetable-main .ui-tt-grid")).toHaveCount(1);
    await openPrintPanel(page);
    const folder = multiStageSchoolPdfFolder(testInfo.outputPath("pdf"));
    const savePdf = async (name: string) => {
      await page.emulateMedia({ media: null }); // page.pdf() applies the print CSS itself
      return page.pdf({ path: `${folder}/${name}.pdf`, preferCSSPageSize: true, printBackground: false });
    };

    // 1. A section on A4 landscape, in colour: subject cells keep their colour although «Background graphics» is off (printBackground: false).
    await expect(page.locator("#print-color")).toHaveValue("color");
    await page.emulateMedia({ media: "print" });
    const filled = page.locator(".print-grid .ui-tt-cell:not(.is-empty)").first();
    expect(await filled.evaluate((cell) => getComputedStyle(cell).backgroundColor)).not.toBe("rgba(0, 0, 0, 0)");
    expect(await filled.evaluate((cell) => getComputedStyle(cell).printColorAdjust)).toBe("exact");
    expect(await smallestFont(page, ".print-grid .ui-tt-subject")).toBeGreaterThanOrEqual(pt(8) - 0.2);
    expect(await smallestFont(page, ".print-grid .ui-tt-teacher")).toBeGreaterThanOrEqual(pt(7) - 0.2);
    const sectionPdf = await savePdf("a4-landscape-section-colour");
    expect(pdfPages(sectionPdf)).toBe(1);

    // 2. The same section in black and white: no fill, bold subject names, borders stay.
    await page.emulateMedia({ media: null });
    await page.locator("#print-color").selectOption("bw");
    await page.emulateMedia({ media: "print" });
    expect(await page.locator(".print-grid .ui-tt-cell:not(.is-empty)").first().evaluate((cell) => getComputedStyle(cell).backgroundColor)).toBe("rgba(0, 0, 0, 0)");
    expect(await page.locator(".print-grid .ui-tt-subject").first().evaluate((node) => Number(getComputedStyle(node).fontWeight))).toBeGreaterThanOrEqual(800);
    expect(await page.locator(".print-grid .ui-tt-grid td").first().evaluate((cell) => getComputedStyle(cell).borderTopWidth)).not.toBe("0px");
    await savePdf("a4-landscape-section-bw");
    await page.emulateMedia({ media: null });
    await page.locator("#print-color").selectOption("color");

    // 3. A teacher on A4 portrait.
    await page.locator("#print-scope").selectOption("teachers");
    await expect(page.locator("#print-orientation")).toHaveValue("portrait");
    const teacherPdf = await savePdf("a4-portrait-teachers");
    expect(pdfPages(teacherPdf)).toBe(12);

    // 4. The whole school on A3 landscape: three stages, a header row each, split over pages by days at a readable size.
    await page.locator("#print-scope").selectOption("school");
    await expect(page.locator("#print-paper")).toHaveValue("A3");
    await expect(page.locator("#print-orientation")).toHaveValue("landscape");
    const schoolPages = Number(await page.locator(".print-document").getAttribute("data-pages"));
    expect(schoolPages).toBe(2); // 5 days × 7 lessons = 36 columns do not fit A3 at 8 pt: 3 days, then 2 days
    await page.emulateMedia({ media: "print" });
    for (const printed of await page.locator(".print-page").all()) {
      await expect(printed.locator(".print-stage-row")).toHaveCount(3);
      expect(await printed.locator(".print-stage").count()).toBe(3);
      await expect(printed.locator(".print-page-header")).toContainText(printing.school);
      await expect(printed.locator(".print-page-footer")).toContainText(printing.signature);
      expect(await printed.locator(".print-master").evaluate((table) => table.scrollWidth <= (table.closest(".print-page") as HTMLElement).clientWidth + 1)).toBe(true);
    }
    await expect(page.locator(".print-page-header").first()).toContainText(printing.daysRange("الأحد", "الثلاثاء"));
    expect(await smallestFont(page, ".print-master .ui-tt-subject")).toBeGreaterThanOrEqual(pt(8) - 0.2);
    expect(await smallestFont(page, ".print-master .ui-tt-teacher")).toBeGreaterThanOrEqual(pt(7) - 0.2);
    // Stage bands: the first column is tinted in colour mode, a thick rule precedes the second stage.
    expect(await page.locator(".print-stage .print-section").first().evaluate((cell) => getComputedStyle(cell).backgroundColor)).not.toBe("rgba(0, 0, 0, 0)");
    expect(await page.locator(".print-stage + .print-stage tr").first().locator("> *").first().evaluate((cell) => parseFloat(getComputedStyle(cell).borderTopWidth))).toBeGreaterThan(2);
    const schoolColour = await savePdf("a3-landscape-school-colour");
    expect(pdfPages(schoolColour)).toBe(schoolPages);

    // 5. Black and white: the stage header text and rules remain, no tint, no fill.
    await page.locator("#print-color").selectOption("bw");
    await page.emulateMedia({ media: "print" });
    expect(await page.locator(".print-stage .print-section").first().evaluate((cell) => getComputedStyle(cell).backgroundColor)).toBe("rgba(0, 0, 0, 0)");
    expect(await page.locator(".print-master td").first().evaluate((cell) => getComputedStyle(cell).backgroundColor)).toBe("rgba(0, 0, 0, 0)");
    await expect(page.locator(".print-stage-row").first()).toContainText("الأول المتوسط");
    await savePdf("a3-landscape-school-bw");
    await page.emulateMedia({ media: null });
    await page.locator("#print-color").selectOption("color");

    // 6. A page per stage: three stages × the day pages, one stage on each page and no stage header row (the title names it).
    await page.locator("#print-scope").selectOption("stages");
    const stagePages = Number(await page.locator(".print-document").getAttribute("data-pages"));
    expect(stagePages).toBe(6);
    await page.emulateMedia({ media: "print" });
    for (const printed of await page.locator(".print-page").all()) {
      expect(await printed.locator(".print-stage").count()).toBe(1);
      await expect(printed.locator(".print-page-header")).toContainText("المرحلة:");
    }
    const stagePdf = await savePdf("a3-landscape-school-per-stage");
    expect(pdfPages(stagePdf)).toBe(stagePages);
  } finally {
    await server.stop();
  }
});
