import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { createFormatter } from "../src/lib/format";
import { ApiServer } from "./support/apiServer";
import { api } from "./support/api";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, fillDate, goToSection, setupOwner } from "./support/flows";

const server = new ApiServer();
const text = messages.school.calendar;
const iraq = text.iraq;

test("(MF8, #87) Iraqi holidays: added with the year, approximate and by-decision marks, disable/enable, re-suggest, date picker, one add entry, month icons, dashboard", async ({ browser, page }, testInfo) => {
  test.setTimeout(180_000);
  await server.start(browser, "mf-holidays");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Holidays-Owner-1");
    await api(page, server.baseUrl, "POST", "/academic-years/", { label: "2026-2027", startDate: "2026-09-01", endDate: "2027-06-30", version: 0 });
    await goToSection(page, messages.school.nav.calendar);

    // One entry point for adding a day; the suggestion card is separate.
    await expect(page.getByRole("button", { name: text.add })).toHaveCount(1);

    // #87: the year was created above, so the Iraqi holidays are ALREADY in the calendar: each Hijri one marked approximate, and the
    // owner-confirmed ones («عيد النصر») marked «قد تُعلن سنوياً بقرار».
    await expect(page.getByRole("row", { name: /عيد النصر/ }).getByText(text.byDecision)).toBeVisible();
    await expect(page.getByRole("row", { name: /عيد الأضحى/ }).getByText(text.approximate)).toBeVisible();
    await page.getByRole("button", { name: iraq.preview }).click();
    const preview = page.getByRole("dialog", { name: iraq.dialogTitle });
    await expect(preview.getByText(iraq.hint)).toBeVisible();
    await expect(preview.getByText(iraq.nothingNew)).toBeVisible();
    await expect(preview.getByText("عيد الأضحى المبارك")).toBeVisible();
    await expect(preview.getByText("اليوم الوطني العراقي")).toBeVisible();
    await expect(preview.getByText(text.byDecision).first()).toBeVisible();
    // Days that stay manual are not suggested.
    await expect(preview.getByText("عيد الجمهورية")).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath("mf8-preview.png"), fullPage: true });
    await preview.getByRole("button", { name: messages.app.cancel }).click();

    // Deleting one (confirmed) and suggesting again offers exactly that one, and adding it back leaves no duplicate.
    const labour = page.getByRole("row", { name: /عيد العمال/ });
    await labour.getByRole("button", { name: new RegExp(`^${messages.school.common.delete}`) }).click();
    await page.getByRole("dialog", { name: text.deleteTitle }).getByRole("button", { name: messages.school.common.delete }).click();
    await expect(page.getByRole("status").filter({ hasText: text.deleted })).toBeVisible();
    await expect(page.getByRole("row", { name: /عيد العمال/ })).toHaveCount(0);
    await page.getByRole("button", { name: iraq.preview }).click();
    await expect(preview.getByText(iraq.nothingNew)).toHaveCount(0);
    await preview.getByRole("button", { name: /^إضافة/ }).click();
    await expect(page.getByRole("status").filter({ hasText: /أُضيفت/ })).toBeVisible();
    await expect(page.getByRole("row", { name: /عيد العمال/ })).toHaveCount(1);

    // The list: kind icon + name, approximate holidays marked, the entries editable/deletable/disable-able.
    const row = page.getByRole("row", { name: /عيد نوروز/ });
    await expect(row.locator(".calendar-kind.kind-officialHoliday")).toBeVisible();
    await expect(page.getByRole("row", { name: /عيد الأضحى/ }).getByText(text.approximate)).toBeVisible();
    await expect(row.getByText(text.approximate)).toHaveCount(0);
    await row.getByRole("button", { name: new RegExp(`^${text.disable}`) }).click();
    await expect(page.getByRole("status").filter({ hasText: text.disabledDone })).toBeVisible();
    await expect(row.getByText(text.disabled)).toBeVisible();
    await row.getByRole("button", { name: new RegExp(`^${text.enable}`) }).click();
    await expect(page.getByRole("status").filter({ hasText: text.enabledDone })).toBeVisible();
    await expectNoSeriousA11yViolations(page, "calendar with holidays");

    // A second suggestion run adds nothing.
    await page.getByRole("button", { name: iraq.preview }).click();
    await expect(page.getByRole("dialog", { name: iraq.dialogTitle }).getByText(iraq.nothingNew)).toBeVisible();
    await page.getByRole("dialog", { name: iraq.dialogTitle }).getByRole("button", { name: messages.app.cancel }).click();

    // Adding a day by choosing the date from the calendar (no typing).
    await page.getByRole("button", { name: text.add }).click();
    const dialog = page.getByRole("dialog", { name: text.add });
    await dialog.getByLabel(text.titleField).fill("يوم المعلم");
    await dialog.getByRole("button", { name: messages.app.datePicker.open }).first().click();
    await dialog.getByRole("button", { name: messages.app.datePicker.nextMonth }).click();
    await dialog.locator(".ui-date-picker-day").nth(14).click();
    await expect(dialog.locator(".ui-date-picker")).toHaveCount(0);
    await expect(dialog.getByRole("textbox", { name: `${text.startDate} - ${messages.app.dateParts.day}`, exact: true })).not.toHaveValue("");
    await dialog.getByLabel(text.kind).selectOption("specialDay");
    await dialog.getByRole("button", { name: text.save }).click();
    await expect(page.getByRole("status").filter({ hasText: text.saved }).or(page.getByText(text.savedOutside))).toBeVisible();

    // Month view: icon and kind colour (class) per entry; Nowruz sits in March 2027.
    await page.getByRole("button", { name: text.monthView }).click();
    const nowruz = page.getByRole("button", { name: "عيد نوروز" });
    const march = createFormatter().month("2027-03-01");
    for (let step = 0; step < 20 && !(await page.locator("#month-title").innerText()).includes(march); step++) {
      await page.getByRole("button", { name: text.nextMonth }).click();
    }
    await expect(nowruz).toBeVisible();
    await expect(nowruz).toHaveClass(/kind-officialHoliday/);
    await page.screenshot({ path: testInfo.outputPath("mf8-month.png"), fullPage: true });
    await expectNoSeriousA11yViolations(page, "calendar month with holidays");
    for (const width of [375, 1024, 1920]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `calendar at ${width}px`);
    }

    // Creating another year from the years page adds its holidays and says so.
    await page.setViewportSize({ width: 1366, height: 900 });
    await goToSection(page, messages.school.nav.academicYears);
    await page.getByRole("button", { name: messages.school.years.add }).first().click();
    const yearDialog = page.getByRole("dialog", { name: messages.school.years.add });
    await yearDialog.getByLabel(messages.school.years.label).fill("2027-2028");
    await fillDate(yearDialog, messages.school.years.startDate, "2027-09-01");
    await fillDate(yearDialog, messages.school.years.endDate, "2028-06-30");
    await yearDialog.getByRole("button", { name: messages.school.years.save }).click();
    await expect(page.getByRole("status").filter({ hasText: messages.school.years.holidaysAdded })).toBeVisible();

    // Dashboard: the next holiday and how far away it is.
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.goto(`${server.baseUrl}/`);
    await expect(page.getByRole("heading", { name: messages.school.dashboard.holiday.title })).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath("mf8-dashboard.png"), fullPage: true });
  } finally {
    await server.stop();
  }
});
