import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, setupOwner } from "./support/flows";
import { writeWorkbook } from "./support/xlsx";

const server = new ApiServer();
const text = messages.school.teachers.import;

const header = ["الاسم الكامل (مطلوب)", "الاسم المختصر", "التخصصات (مواد مفصولة بـ «،»)", "الحد الأقصى للحصص يومياً", "الحد الأقصى للحصص أسبوعياً", "ملاحظات"];

test("(M2) Excel import of teachers: template, dry-run preview with Arabic notes, valid rows only, idempotent re-import", async ({ browser, page }, testInfo) => {
  test.setTimeout(120_000);
  await server.start(browser, "m2-import");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Import-Owner-1");
    await page.goto(`${server.baseUrl}/teachers/list`);
    await page.getByRole("button", { name: text.button }).click();
    await expect(page.getByRole("heading", { name: text.title })).toBeVisible();

    // The template downloads as a workbook.
    const [template] = await Promise.all([page.waitForEvent("download"), page.getByRole("link", { name: text.downloadTemplate }).click()]);
    expect(template.suggestedFilename()).toBe("teachers-template.xlsx");

    const file = writeWorkbook(testInfo.outputPath("files"), "teachers", "المعلمون", [
      header,
      ["ليلى حسين كاظم", "", "", "6", "24", "ملاحظة"],
      ["ليلى  حسين كاظم", "", "", "", "", ""],
      ["", "اسم بلا معلم", "", "", "", ""],
      ["رائد جاسم", "", "مادة غير موجودة", "", "", ""],
      ["منى سالم", "", "", "ثلاثة", "", ""],
    ]);
    await page.locator("#teachers-import-file").setInputFiles(file);

    // The dry run: every row has an outcome and nothing is saved yet.
    const table = page.getByRole("table", { name: text.preview });
    await expect(table.locator("tbody tr")).toHaveCount(5);
    await expect(page.getByRole("status").filter({ hasText: text.summary("٢", "٠", "٣", "١") })).toBeVisible();
    await expect(table.getByText(text.issues.duplicateInFile("٢"))).toBeVisible();
    await expect(table.getByText(text.issues.nameMissing).first()).toBeVisible();
    await expect(table.getByText(text.issues.unknownSubject("مادة غير موجودة"))).toBeVisible();
    await expect(table.getByText(text.issues.limitInvalid("day"))).toBeVisible();
    await expectNoSeriousA11yViolations(page, "teachers import preview");
    await page.screenshot({ path: testInfo.outputPath("import-preview.png"), fullPage: true });
    for (const width of [375, 1024]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `teachers import at ${width}px`);
    }
    await page.setViewportSize({ width: 1366, height: 900 });
    await expect(page.getByRole("list", { name: messages.school.teachers.title }).getByText("ليلى حسين كاظم")).toHaveCount(0); // not a teacher yet

    // Apply: only the valid rows (two of five) are added.
    await page.getByRole("button", { name: text.apply("٢") }).click();
    await expect(page.getByRole("status").filter({ hasText: text.done("٢", "٠", "٣") })).toBeVisible();
    await expect(page.getByRole("heading", { name: text.title })).toBeHidden();
    await expect(page.getByText("ليلى حسين كاظم").first()).toBeVisible();
    await expect(page.getByText("رائد جاسم").first()).toBeVisible();

    // The same file again: nothing new, nothing overwritten, the import button is off.
    await page.getByRole("button", { name: text.button }).click();
    await page.locator("#teachers-import-file").setInputFiles(file);
    await expect(table.locator("tbody tr")).toHaveCount(5);
    await expect(page.getByRole("status").filter({ hasText: text.summary("٠", "٢", "٣", "٠") })).toBeVisible();
    await expect(page.getByRole("button", { name: text.apply("٠") })).toBeDisabled();
    await expect(page.getByText(text.nothingReady)).toBeVisible();
  } finally {
    await server.stop();
  }
});
