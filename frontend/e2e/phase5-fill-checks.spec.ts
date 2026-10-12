import { expect, test } from "@playwright/test";
import { isolate } from "../src/i18n/isolate";
import { messages } from "../src/i18n/messages";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

const server = new ApiServer();
const readiness = messages.school.readiness;
const profile = messages.school.schedulingProfile;

type Subject = { id: number };
type Teacher = { id: number };
type Curriculum = { stages: Array<{ id: number }> };

test("(fill checks) a teacher off on Sunday cannot give 10 lessons at 2 a day; raising the subject's daily limit fixes it", async ({ browser, page }) => {
  test.setTimeout(180_000);
  await server.start(browser, "phase5-fill-checks");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Fill-Checks-1");
    const { yearId } = await prepareSchool(page, server.baseUrl, {
      schoolType: "intermediate", shiftMode: "morning", grades: [{ gradeKey: "intermediate-1", sections: 1 }],
    });
    const arabic = await api<Subject>(page, server.baseUrl, "POST", "/subjects/", {
      name: "العربي", colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false,
      requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
    });
    const curriculum = await api<Curriculum>(page, server.baseUrl, "GET", `/academic-years/${yearId}/curriculum`);
    const stageId = curriculum.stages[0].id;
    await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/curriculum/cell`, {
      stageId, subjectId: arabic.id, label: null, weeklyLessons: 10, entryId: null, version: null,
    });
    // Sunday is ISO day 7: the teacher is off that day.
    const teacher = await api<Teacher>(page, server.baseUrl, "POST", "/teachers/", {
      fullName: "رغد", shortName: "رغد", offDays: [7], blockedPeriods: [], fullyReleased: false,
      releaseReason: null, releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null,
      notes: null, version: 0, specializationIds: [arabic.id],
    });
    const matrix = await api<{ stage: { sections: Array<{ sectionId: number }> } }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/workload/matrix?stageId=${stageId}`);
    await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/workload/bulk/class-teacher`, {
      teacherId: teacher.id, sectionId: matrix.stage.sections[0].sectionId, entryIds: [], overwrite: false,
    });

    // The report names the subject, the section, the number of days, the daily limit, the teacher and the day.
    await page.goto(`${server.baseUrl}/readiness`);
    const message = readiness.messages.SUBJECT_DAYS_SHORT(
      isolate("العربي"), isolate("الأول المتوسط / أ"), "٥ أيام", "١٠ حصص", "حصتان", isolate("رغد"), "٤ أيام", readiness.daysOff("الأحد"),
    );
    await expect(page.getByText(message)).toBeVisible();
    await expectNoSeriousA11yViolations(page, "fill-check readiness");

    // The fix link leads to «أولويات الجدول › متقدم»: the owner raises the limit of the subject to 3 a day.
    await page.getByRole("link", { name: readiness.fixes.raiseDailyCap }).first().click();
    await expect(page.getByRole("heading", { name: profile.dailyCap.title })).toBeVisible();
    await page.getByRole("combobox", { name: profile.dailyCap.chooseFor("العربي") }).selectOption("3");
    await expect(page.getByRole("status").filter({ hasText: profile.dailyCap.saved })).toBeVisible();

    await page.goto(`${server.baseUrl}/readiness`);
    await expect(page.getByText(readiness.ready)).toBeVisible();
    await expect(page.getByText(message)).toHaveCount(0);
  } finally {
    await server.stop();
  }
});
