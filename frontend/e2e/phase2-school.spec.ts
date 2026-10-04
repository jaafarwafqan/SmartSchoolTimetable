import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import {
  expectBreakpointScreenshots,
  expectNoSeriousA11yViolations,
  goToSection,
  openUserMenuItem,
  setupOwner,
  tinyPng,
} from "./support/flows";

// Phase 2 end-to-end flow against the real API and a temporary database (checkpoint 2A scope).
const server = new ApiServer();
const school = messages.school;
const structure = school.scheduleStructure;
const stages = school.stagesSections;
const subjects = school.subjects;
const teachers = school.teachers;
const calendar = school.calendar;
const password = "Phase2-Owner-1";
const schoolName = "إعدادية النور للبنات";

test.describe.configure({ mode: "serial" });

test.beforeAll(async ({ browser }) => {
  await server.start(browser, "phase2");
});

test.afterAll(async () => {
  await server.stop();
});

test("the whole setup checklist completes end to end; stale edits are caught", async ({ page, context }) => {
  await setupOwner(page, server.baseUrl, "owner", password);
  await expect(page.locator(".checklist-label", { hasText: school.dashboard.steps.schoolProfile })).toBeVisible();
  await expect(page.getByRole("link", { name: new RegExp(school.dashboard.openStep) }).first()).toBeVisible();
  await expectNoSeriousA11yViolations(page, "dashboard (empty)");

  // School profile: inline validation, save, upload a real image.
  await goToSection(page, school.nav.profile);
  await page.getByRole("button", { name: school.profile.save }).click();
  await expect(page.getByText(messages.errors.REQUIRED)).toBeVisible();
  await expect(page.getByLabel(school.profile.name)).toBeFocused();
  await page.getByLabel(school.profile.name).fill(schoolName);
  await page.getByLabel(school.profile.schoolType).selectOption("preparatory");
  await page.getByLabel(school.profile.numeralSystem).selectOption("western");
  await page.getByRole("button", { name: school.profile.save }).click();
  await expect(page.getByRole("status").filter({ hasText: school.profile.saved })).toBeVisible();
  await expect(page.locator(".app-topbar")).toContainText(schoolName);
  await page.locator("#logo-file").setInputFiles({ name: "logo.png", mimeType: "image/png", buffer: tinyPng });
  await expect(page.getByRole("img", { name: school.profile.imageAlt(school.profile.logo) })).toBeVisible();
  await page.locator("#stamp-file").setInputFiles({ name: "stamp.svg", mimeType: "image/svg+xml", buffer: Buffer.from("<svg xmlns='http://www.w3.org/2000/svg'/>") });
  await expect(page.getByRole("alert")).toContainText(messages.errors.ASSET_TYPE_NOT_ALLOWED);
  await expectNoSeriousA11yViolations(page, "school profile");

  // Two pages edit the same profile: the second save is rejected with an Arabic conflict message and reload.
  const other = await context.newPage();
  await other.goto(`${server.baseUrl}/school`);
  await expect(other.getByLabel(school.profile.name)).toHaveValue(schoolName);
  await page.reload();
  await page.getByLabel(school.profile.principalName).fill("أ. زينب");
  await page.getByRole("button", { name: school.profile.save }).click();
  await expect(page.getByRole("status").filter({ hasText: school.profile.saved })).toBeVisible();
  await other.getByLabel(school.profile.principalName).fill("أ. حسن");
  await other.getByRole("button", { name: school.profile.save }).click();
  await expect(other.getByText(school.common.conflictTitle)).toBeVisible();
  await other.getByRole("button", { name: school.common.reload }).click();
  await expect(other.getByLabel(school.profile.principalName)).toHaveValue("أ. زينب");
  await other.close();

  // Academic year and terms.
  await goToSection(page, school.nav.academicYears);
  await expect(page.getByText(school.years.empty)).toBeVisible();
  await page.getByRole("button", { name: school.years.add }).first().click();
  const yearDialog = page.getByRole("dialog", { name: school.years.add });
  await yearDialog.getByLabel(school.years.label).fill("2026-2027");
  await yearDialog.getByLabel(school.years.startDate).fill("2026-09-01");
  await yearDialog.getByLabel(school.years.endDate).fill("2026-08-01");
  await yearDialog.getByRole("button", { name: school.years.save }).click();
  await expect(yearDialog.getByText(messages.errors.INVALID_DATE_RANGE)).toBeVisible();
  await yearDialog.getByLabel(school.years.endDate).fill("2027-06-30");
  await yearDialog.getByRole("button", { name: school.years.save }).click();
  await expect(page.getByRole("status").filter({ hasText: school.years.saved })).toBeVisible();

  await page.getByRole("button", { name: school.years.addTerm }).click();
  const termDialog = page.getByRole("dialog", { name: school.years.addTerm });
  await termDialog.getByLabel(school.years.termName).fill("الفصل الأول");
  await termDialog.getByLabel(school.years.endDate).fill("2027-01-15");
  await termDialog.getByRole("button", { name: school.years.saveTerm }).click();
  await page.getByRole("button", { name: `${school.years.makeCurrentTerm}: ${"⁨"}الفصل الأول${"⁩"}` }).click();
  await expect(page.getByText(school.years.currentTerm, { exact: true })).toBeVisible();
  await expect(page.locator(".app-topbar")).toContainText("الفصل الأول");
  await expectNoSeriousA11yViolations(page, "academic years");

  // Timetable structure (2B): a shift, generator validation, a row-level overlap error, then a valid save.
  await goToSection(page, school.nav.scheduleStructure);
  await page.getByRole("button", { name: structure.addShift }).first().click();
  const shiftDialog = page.getByRole("dialog", { name: structure.addShift });
  await shiftDialog.getByLabel(structure.shiftName).fill("صباحي");
  await shiftDialog.getByRole("button", { name: structure.saveShift }).click();
  await expect(page.getByRole("status").filter({ hasText: structure.shiftSaved })).toBeVisible();
  await page.getByRole("button", { name: structure.generate }).click();
  const generator = page.getByRole("dialog", { name: structure.generate });
  await generator.getByLabel(structure.lessonCount).fill("13");
  await generator.getByRole("button", { name: structure.createList }).click();
  await expect(generator.getByText(messages.errors.VALUE_OUT_OF_RANGE)).toBeVisible();
  await generator.getByLabel(structure.lessonCount).fill("6");
  await generator.getByRole("button", { name: structure.createList }).click();
  await expect(page.locator(".period-row")).toHaveCount(7);
  const secondStart = page.getByLabel(`${structure.startTime} - ${structure.rowLabel("2")}`);
  await secondStart.fill("08:10");
  await page.getByRole("button", { name: structure.savePeriods }).click();
  await expect(page.locator(".period-row-error")).toContainText(messages.errors.PERIODS_OVERLAP);
  await secondStart.fill("08:45");
  await page.getByRole("button", { name: structure.savePeriods }).click();
  await expect(page.getByRole("status").filter({ hasText: structure.periodsSaved })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "timetable structure");
  await expectBreakpointScreenshots(page, "periods");

  // Stages and sections (2C): weekly capacity = 5 working days × 6 lessons.
  await goToSection(page, school.nav.stagesSections);
  await page.getByRole("button", { name: stages.addStage }).first().click();
  const stageDialog = page.getByRole("dialog", { name: stages.addStage });
  await stageDialog.getByLabel(stages.stageName).fill("الأول المتوسط");
  await stageDialog.getByRole("button", { name: stages.saveStage }).click();
  await expect(page.getByRole("status").filter({ hasText: stages.stageSaved })).toBeVisible();
  await page.getByRole("button", { name: stages.addSection }).first().click();
  const sectionDialog = page.getByRole("dialog", { name: stages.addSection });
  await sectionDialog.getByLabel(stages.label).fill("أ");
  await sectionDialog.getByRole("button", { name: stages.saveSection }).click();
  await expect(page.getByRole("status").filter({ hasText: stages.sectionSaved })).toBeVisible();
  await expect(page.getByRole("cell", { name: "30", exact: true })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "stages and sections");

  // Subjects (2D): palette colour, priority, a blocked period toggled with the keyboard.
  await goToSection(page, school.nav.subjects);
  await page.getByRole("button", { name: subjects.add }).first().click();
  const subjectDialog = page.getByRole("dialog", { name: subjects.add });
  await subjectDialog.getByLabel(subjects.name).fill("الرياضيات");
  await subjectDialog.getByLabel(subjects.colorSwatch("5")).check();
  await subjectDialog.getByLabel(subjects.priority).selectOption("5");
  await subjectDialog.getByText(subjects.heavy).click();
  const firstCell = subjectDialog.getByRole("button", { name: school.blockedGrid.cell("الأحد", "1", false) });
  await firstCell.focus();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Space");
  await expect(subjectDialog.getByRole("button", { name: school.blockedGrid.cell("الاثنين", "1", true) })).toHaveAttribute("aria-pressed", "true");
  await expectNoSeriousA11yViolations(page, "subject dialog");
  await subjectDialog.getByRole("button", { name: subjects.save }).click();
  await expect(page.getByRole("status").filter({ hasText: subjects.saved })).toBeVisible();
  await expect(page.getByRole("cell", { name: "الرياضيات", exact: true })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "subjects");

  // Teachers (2E): limits are checked against 6 lessons per day; bulk add previews before saving.
  await goToSection(page, school.nav.teachers);
  await page.getByRole("button", { name: teachers.add }).first().click();
  const teacherDialog = page.getByRole("dialog", { name: teachers.add });
  await teacherDialog.getByLabel(teachers.fullName).fill("زينب كاظم جواد");
  await teacherDialog.getByLabel(teachers.shortName).fill("زينب كاظم");
  await teacherDialog.getByRole("group", { name: teachers.offDays }).getByText(school.scheduleStructure.days.thursday).click();
  await teacherDialog.getByLabel(`${teachers.limits} ${teachers.maxPerDay}`).fill("7");
  await teacherDialog.getByRole("button", { name: teachers.save }).click();
  await expect(teacherDialog.getByText(messages.errors.MAX_PER_DAY_EXCEEDS_PERIODS)).toBeVisible();
  await expect(teacherDialog.getByLabel(`${teachers.limits} ${teachers.maxPerDay}`)).toBeFocused();
  await teacherDialog.getByLabel(`${teachers.limits} ${teachers.maxPerDay}`).fill("5");
  await teacherDialog.getByText(teachers.fullyReleased).click();
  await expect(teacherDialog.getByLabel(teachers.releaseReason)).toBeVisible();
  await teacherDialog.getByText(teachers.fullyReleased).click();
  await expectNoSeriousA11yViolations(page, "teacher dialog");
  await teacherDialog.getByRole("button", { name: teachers.save }).click();
  await expect(page.getByRole("status").filter({ hasText: teachers.saved })).toBeVisible();

  await page.getByRole("button", { name: teachers.bulkAdd }).click();
  const bulkDialog = page.getByRole("dialog", { name: teachers.bulkTitle });
  await bulkDialog.getByLabel(teachers.bulkNames).fill("حسن علي مهدي\nزينب كاظم جواد\n\nمريم عباس");
  await bulkDialog.getByRole("button", { name: teachers.bulkPreview }).click();
  await expect(bulkDialog.getByText(teachers.bulkStatuses.exists)).toBeVisible();
  await expectNoSeriousA11yViolations(page, "bulk add preview");
  await bulkDialog.getByRole("button", { name: teachers.bulkSave("2") }).click();
  await expect(page.getByRole("status").filter({ hasText: teachers.bulkSaved("2") })).toBeVisible();
  await expect(page.getByRole("cell", { name: "حسن علي", exact: true })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "teachers");
  await expectBreakpointScreenshots(page, "teachers");

  // Academic calendar (2F): an entry outside the current year is saved with a warning; the month view shows it.
  await goToSection(page, school.nav.calendar);
  await page.getByRole("button", { name: calendar.add }).first().click();
  const dayDialog = page.getByRole("dialog", { name: calendar.add });
  await dayDialog.getByLabel(calendar.titleField).fill("عطلة صيفية");
  await dayDialog.getByLabel(calendar.startDate).fill("2027-07-10");
  await dayDialog.getByRole("button", { name: calendar.save }).click();
  await expect(page.getByText(calendar.savedOutside)).toBeVisible();
  await page.getByRole("button", { name: calendar.add }).first().click();
  const secondDialog = page.getByRole("dialog", { name: calendar.add });
  await secondDialog.getByLabel(calendar.titleField).fill("يوم المعلم");
  await secondDialog.getByLabel(calendar.startDate).fill("2027-03-01");
  await secondDialog.getByLabel(calendar.kind).selectOption("specialDay");
  await secondDialog.getByRole("button", { name: calendar.save }).click();
  await expect(page.getByRole("status").filter({ hasText: calendar.saved })).toBeVisible();
  await expect(page.getByText(calendar.outsideYear)).toBeVisible();
  await expectNoSeriousA11yViolations(page, "calendar list");
  await page.getByRole("button", { name: calendar.monthView }).click();
  for (let step = 0; step < 5; step++) await page.getByRole("button", { name: calendar.nextMonth }).click();
  await expect(page.getByRole("button", { name: "يوم المعلم" })).toBeVisible();
  await expectNoSeriousA11yViolations(page, "calendar month");

  // Dashboard reflects the completed steps and real counts.
  await goToSection(page, school.nav.dashboard);
  await expect(page.getByText(school.dashboard.checklistDone)).toBeVisible();
  await expect(page.locator(".count-item")).toHaveText([/3/, /1/, /1/, /1/, /1/, /0/]); // teachers, years, stages, sections, subjects, capacity gaps
  await expectNoSeriousA11yViolations(page, "dashboard (complete)");
  await expectBreakpointScreenshots(page, "dashboard");

  // A new year copies the structure of the previous one (shifts, periods, stages, sections), never calendar days.
  await goToSection(page, school.nav.academicYears);
  await page.getByRole("button", { name: school.years.add }).first().click();
  const nextYear = page.getByRole("dialog", { name: school.years.add });
  await nextYear.getByLabel(school.years.label).fill("2027-2028");
  await nextYear.getByLabel(school.years.startDate).fill("2027-09-01");
  await nextYear.getByLabel(school.years.endDate).fill("2028-06-30");
  await nextYear.getByLabel(school.years.copyFrom).selectOption({ label: "2026-2027" });
  await nextYear.getByRole("button", { name: school.years.save }).click();
  await expect(page.getByRole("status").filter({ hasText: school.years.saved })).toBeVisible();
  await goToSection(page, school.nav.stagesSections);
  await page.getByLabel(stages.year).selectOption({ label: "2027-2028" });
  await expect(page.getByRole("cell", { name: "الأول المتوسط", exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: "30", exact: true })).toBeVisible();

  // Lock screen keeps the username and asks only for the password.
  await openUserMenuItem(page, school.shell.lock);
  await expect(page.getByText(school.shell.lockedNotice)).toBeVisible();
  await expect(page.getByLabel(messages.app.username)).toHaveValue("owner");
});
