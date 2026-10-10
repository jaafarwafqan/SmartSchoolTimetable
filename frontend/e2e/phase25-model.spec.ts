import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { arabicCount } from "../src/lib/arabicCount";
import { createFormatter, formatNumber } from "../src/lib/format";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { expectNo24HourTimes, expectNoSeriousA11yViolations, expectNoTextOverlap, goToSection, setupOwner } from "./support/flows";

// Owner model changes: M1 editable breaks (ADR 0026) and M2 lessons per day per stage (ADR 0027).
const server = new ApiServer();
const school = messages.school;
const wizard = school.wizard;
const shiftText = school.shiftSystem;
const breaks = school.scheduleStructure.breaks;
const cards = school.stageCards;
const curriculum = school.curriculum;
const format = createFormatter(); // a new school: Arabic-Indic digits, Gregorian calendar
const arab = (value: number) => formatNumber(value, "arab");

test.describe.configure({ mode: "serial" });
test.beforeAll(async ({ browser }) => { await server.start(browser, "phase25-model"); });
test.afterAll(async () => { await server.stop(); });

test("breaks are edited per shift and stages get their own lessons per day", async ({ page }) => {
  await setupOwner(page, server.baseUrl, "owner", "Model-Owner-1");
  const { yearId } = await prepareSchool(page, server.baseUrl, {
    schoolType: "primary", shiftMode: "morning",
    grades: [{ gradeKey: "primary-1", sections: 2 }, { gradeKey: "primary-6", sections: 1 }],
    subjects: ["الرياضيات"],
  });

  // M1: wizard step 3 edits the break length and adds breaks; the preview follows live.
  await page.goto(`${server.baseUrl}/setup`);
  await page.getByRole("button", { name: wizard.back }).click();
  await expect(page.getByRole("heading", { level: 2, name: wizard.steps[3] })).toBeVisible();
  const preview = page.getByRole("list", { name: shiftText.preview(shiftText.sessions.morning) });
  const rowsOf = () => preview.getByRole("listitem");
  const fourthLesson = rowsOf().filter({ hasText: `الحصة ${arab(4)}:` });
  await expect(rowsOf()).toHaveCount(8); // 7 lessons + 1 break
  await expect(fourthLesson).toContainText(format.time("10:30"));
  await expectNo24HourTimes(page, "wizard timing step"); // R1: the preview runs past noon («١:٣٠ م», never 13:30)
  // R2: a quick pick sets the first break to 20 minutes.
  await page.getByRole("group", { name: breaks.quickPicks(arab(1)) }).getByRole("button", { name: format.count(20, "minute") }).click();
  await expect(fourthLesson).toContainText(format.time("10:35"));
  await page.getByRole("button", { name: breaks.add }).click(); // after lesson 4
  await expect(rowsOf()).toHaveCount(9);
  // R2: no cap of three; a third break moved after lesson 1 and shortened to one minute with the stepper.
  await page.getByRole("button", { name: breaks.add }).click(); // after lesson 5
  await page.getByLabel(breaks.after(arab(3))).selectOption("1");
  await page.getByRole("spinbutton", { name: breaks.duration(arab(1)) }).press("Home");
  await expect(page.locator(".shift-system-block .breaks-clock").first()).toHaveText(breaks.clock(format.time("08:45"), format.time("08:46")));
  await expect(rowsOf()).toHaveCount(10);
  // R2: lowering the lessons to 4 leaves the break after lesson 4 after the last lesson: an Arabic message, «التالي» refused.
  for (let step = 0; step < 3; step++) await page.getByRole("button", { name: shiftText.lessonsDecrease }).click();
  await expect(page.getByText(breaks.afterLast)).toBeVisible();
  await page.getByRole("button", { name: wizard.next }).click();
  await expect(page.getByText(breaks.blocked)).toBeVisible();
  for (let step = 0; step < 3; step++) await page.getByRole("button", { name: shiftText.lessonsIncrease }).click();
  await expect(page.getByText(breaks.afterLast)).toBeHidden();
  await page.getByLabel(breaks.gap).selectOption("5");
  await expect(rowsOf().filter({ hasText: `الحصة ${arab(2)}:` })).toContainText(format.time("08:46"));
  await expectNoSeriousA11yViolations(page, "wizard step 3 with the breaks editor");
  await expectNoTextOverlap(page.locator(".shift-system-block").first(), "breaks editor and preview");
  await page.getByRole("button", { name: wizard.next }).click();
  await expect(page.getByRole("heading", { level: 2, name: wizard.steps[4] })).toBeVisible();
  const shifts = await api<{ items: { id: number; periods: { kind: string; startTime: string; endTime: string }[] }[] }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/shifts/?pageSize=100`);
  const savedBreaks = shifts.items[0].periods.filter((period) => period.kind === "break");
  expect(savedBreaks).toHaveLength(3);
  expect(savedBreaks[0]).toMatchObject({ startTime: "08:45", endTime: "08:46" }); // after lesson 1, one minute
  expect(savedBreaks[1]).toMatchObject({ startTime: "10:21", endTime: "10:41" }); // lessons 2–3 with a 5-minute gap, then 20 minutes

  // M2: the first grade teaches six lessons a day; its capacity drops, the other stage keeps the shift's 35.
  const firstCard = page.locator(".stage-card", { hasText: "الأول الابتدائي" });
  await firstCard.getByRole("button", { name: cards.dailyDecrease("الأول الابتدائي") }).click();
  await expect(page.getByRole("status").filter({ hasText: cards.lessonsSaved("الأول الابتدائي") })).toBeVisible();
  await expect(firstCard).toContainText(cards.ownCounts);
  await expect(firstCard.locator(".stage-card-capacity")).toContainText(arabicCount(30, "lesson", arab));
  await expect(page.locator(".stage-card", { hasText: "السادس الابتدائي" }).locator(".stage-card-capacity")).toContainText(arabicCount(35, "lesson", arab));
  await firstCard.getByText(cards.perDay).click();
  await firstCard.getByRole("button", { name: cards.dayDecrease("الأول الابتدائي", school.scheduleStructure.days.thursday) }).click();
  await expect(firstCard.locator(".stage-card-capacity")).toContainText(arabicCount(29, "lesson", arab));
  await expectNoSeriousA11yViolations(page, "stage cards with lessons per stage");
  await expectNoTextOverlap(page.locator(".stage-cards"), "stage cards with lessons per stage");

  // The curriculum compares each stage with its own capacity.
  await goToSection(page, school.nav.curriculum);
  const headers = page.locator(".curriculum-table thead th.curriculum-stage-head");
  await expect(headers.first()).toContainText(curriculum.headerCapacity(arab(29)));
  await expect(headers.nth(1)).toContainText(curriculum.headerCapacity(arab(35)));
  await expect(page.locator(".curriculum-table tfoot td").first()).toContainText(curriculum.status.under(arab(29)));

  // Shortening the shift on Thursday below the stage's five lessons is shown and confirmed first.
  await goToSection(page, school.nav.scheduleStructure);
  const thursday = school.scheduleStructure.days.thursday;
  for (let step = 0; step < 3; step++) await page.getByRole("button", { name: school.scheduleStructure.decreaseFor(thursday) }).click();
  await page.getByRole("button", { name: school.scheduleStructure.saveDayLessons }).click();
  const dialog = page.getByRole("dialog", { name: school.scheduleStructure.stageImpact.title });
  await expect(dialog).toContainText("الأول الابتدائي");
  await expectNoSeriousA11yViolations(page, "shorten shift confirmation");
  await dialog.getByRole("button", { name: school.scheduleStructure.stageImpact.confirm }).click();
  await expect(page.getByRole("status").filter({ hasText: school.scheduleStructure.dayLessonsSaved })).toBeVisible();
  const stages = await api<{ stage: { name: string; dayLessons: { day: number; lessons: number }[] } }[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/stage-cards`);
  expect(stages[0].stage.dayLessons.find((entry) => entry.day === 4)?.lessons).toBe(4);
});
