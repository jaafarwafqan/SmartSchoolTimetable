import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { arabicCount } from "../src/lib/arabicCount";
import { createFormatter, formatNumber } from "../src/lib/format";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { expectNoSeriousA11yViolations, expectNoTextOverlap, goToSection, setupOwner } from "./support/flows";

// Owner model changes: M1 editable breaks (ADR 0026) and M2 lessons per day per stage (ADR 0027).
const server = new ApiServer();
const school = messages.school;
const wizard = school.wizard;
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
  const preview = page.getByRole("table", { name: wizard.timing.previewTitle(wizard.timing.shifts.morning) });
  const fourthLesson = preview.getByRole("row", { name: new RegExp(wizard.timing.lessonRow(arab(4))) });
  await expect(preview.getByRole("row")).toHaveCount(9); // header + 7 lessons + 1 break
  await expect(fourthLesson).toContainText(format.time("10:30"));
  await page.getByLabel(breaks.duration(arab(1))).selectOption("20");
  await expect(fourthLesson).toContainText(format.time("10:35"));
  await page.getByRole("button", { name: breaks.add }).click();
  await expect(preview.getByRole("row")).toHaveCount(10);
  await page.getByLabel(breaks.gap).selectOption("5");
  await expect(preview.getByRole("row", { name: new RegExp(wizard.timing.lessonRow(arab(2))) })).toContainText(format.time("08:50"));
  await expectNoSeriousA11yViolations(page, "wizard step 3 with the breaks editor");
  await expectNoTextOverlap(page.locator(".wizard-shift").first(), "breaks editor and preview");
  await page.getByRole("button", { name: wizard.next }).click();
  await expect(page.getByRole("heading", { level: 2, name: wizard.steps[4] })).toBeVisible();
  const shifts = await api<{ items: { id: number; periods: { kind: string; startTime: string; endTime: string }[] }[] }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/shifts/?pageSize=100`);
  const savedBreaks = shifts.items[0].periods.filter((period) => period.kind === "break");
  expect(savedBreaks).toHaveLength(2);
  expect(savedBreaks[0]).toMatchObject({ startTime: "10:25", endTime: "10:45" }); // 3 lessons + 2 gaps of 5, then 20 minutes

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
