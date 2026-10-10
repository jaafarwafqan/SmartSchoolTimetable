import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { api } from "./support/api";
import { seedReadySchool } from "./support/readySchool";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

const server = new ApiServer();
const check = messages.school.currentCheck;
const timetable = messages.school.timetable;
const generation = messages.school.generation;

type Summary = { id: number; version: number; status: string };
type Lesson = { sectionId: number; lineId: number; subjectId: number; teacherId: number; day: number; lesson: number };
type Teacher = { id: number; fullName: string; shortName: string; version: number; blockedPeriods: unknown[]; offDays: number[]; specializationIds?: number[] };

async function generate(page: Page, yearId: number): Promise<Summary> {
  const run = await api<{ id: number }>(page, server.baseUrl, "POST", `/academic-years/${yearId}/generation/runs`, { timeLimitSeconds: 10, deterministic: true, seed: 3 });
  for (let attempt = 0; attempt < 200; attempt++) {
    const state = await api<{ status: string }>(page, server.baseUrl, "GET", `/generation/runs/${run.id}`);
    if (state.status === "completed") break;
    expect(["queued", "validating", "generating"]).toContain(state.status);
    await page.waitForTimeout(500);
  }
  return (await api<Summary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`))[0];
}

async function lessonsOf(page: Page, versionId: number): Promise<Lesson[]> {
  return (await api<{ lessons: Lesson[] }>(page, server.baseUrl, "GET", `/timetables/${versionId}`)).lessons;
}

test("(MF11) a blocked day: conflicts in plain Arabic, cells marked, approval blocked, repair moves only those lessons", async ({ browser, page }, testInfo) => {
  test.setTimeout(240_000);
  await server.start(browser, "mf-currentdata-block");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Current-Owner-1");
    const yearId = await seedReadySchool(page, server.baseUrl);
    const version = await generate(page, yearId);
    const lessons = await lessonsOf(page, version.id);

    // Nothing changed: the page says it still fits, and labels the snapshot check separately.
    await page.goto(`${server.baseUrl}/timetable/view?version=${version.id}`);
    await expect(page.getByText(timetable.verified)).toBeVisible();
    await expect(page.locator(".current-check").getByText(check.status.fits)).toBeVisible();
    await expect(page.getByRole("button", { name: check.repair })).toHaveCount(0);
    await expect(page.getByRole("button", { name: timetable.approve })).toBeEnabled();

    // Block the busiest teacher-day through the normal teachers API.
    const counts = new Map<string, number>();
    for (const lesson of lessons) counts.set(`${lesson.teacherId}:${lesson.day}`, (counts.get(`${lesson.teacherId}:${lesson.day}`) ?? 0) + 1);
    const [busiest] = [...counts.entries()].sort((a, b) => b[1] - a[1]);
    const [teacherId, day] = busiest[0].split(":").map(Number);
    const teachers = await api<{ items: Teacher[] }>(page, server.baseUrl, "GET", "/teachers/?pageSize=100");
    const teacher = teachers.items.find((item) => item.id === teacherId)!;
    await api(page, server.baseUrl, "PUT", `/teachers/${teacherId}`, {
      fullName: teacher.fullName, shortName: teacher.shortName, offDays: [day], blockedPeriods: teacher.blockedPeriods, fullyReleased: false, releaseReason: null,
      releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null, notes: null, version: teacher.version,
      specializationIds: teacher.specializationIds ?? [],
    });

    await page.reload();
    const panel = page.locator(".current-check");
    await expect(panel.locator(".ui-badge").first()).toContainText("يتعارض مع بيانات المدرسة الحالية");
    await expect(panel.locator(".current-findings li").first()).toContainText("غير متاح");
    await expect(page.getByText(timetable.verified)).toBeVisible(); // the generation-time check is unchanged
    await expect(page.getByRole("button", { name: timetable.approve })).toBeDisabled();
    await expect(page.getByText(check.approvalBlocked)).toBeVisible();
    await expect(panel.getByText(check.changesTitle)).toBeVisible();

    // «عرض في الجدول» switches to the section and focuses a conflicting cell (icon and border, not colour only).
    await panel.locator(".current-findings li").first().getByRole("button", { name: check.show }).click();
    await expect(page.locator(".timetable-views .ui-tt-cell.is-conflict").first()).toBeVisible();
    await expect(page.locator(".timetable-views .ui-tt-cell.is-conflict .ui-tt-conflict-icon").first()).toBeVisible();
    await expect(page.locator(".timetable-views .ui-tt-cell.is-conflict").first()).toBeFocused();
    await expectNoSeriousA11yViolations(page, "timetable with current-data conflicts");
    for (const width of [375, 1024, 1920]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `current-data check at ${width}px`);
    }
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.screenshot({ path: testInfo.outputPath("mf11-conflicts.png"), fullPage: true });

    // The repair runs on the generation page and ends in a new draft that fits and can be approved.
    await page.getByRole("button", { name: check.repair }).click();
    await expect(page).toHaveURL(/\/timetable\/generate/);
    await expect(page.getByRole("heading", { name: generation.resultTitle })).toBeVisible({ timeout: 90_000 });
    await expect(page.getByText(check.repairRun.title)).toBeVisible();
    await expect(page.getByText(/الحصص التي تغيّرت/)).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath("mf11-repair-result.png"), fullPage: true });
    await page.getByRole("link", { name: generation.openTimetable }).click();
    await expect(page.locator(".current-check").getByText(check.status.fits)).toBeVisible();
    await expect(page.locator("#timetable-version option:checked")).toContainText(timetable.sources.repaired);
    await page.getByRole("button", { name: timetable.approve }).click();
    await page.getByRole("dialog").getByRole("button", { name: timetable.approve }).click();
    await expect(page.getByText(timetable.approvedDone).or(page.getByText(messages.school.lifecycle.approveDoneArchived))).toBeVisible();

    // Only the conflicting lessons changed: every other lesson of the first version is in the repaired one, unchanged.
    const versionsNow = await api<Summary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`);
    const repaired = await lessonsOf(page, versionsNow[0].id);
    const key = (lesson: Lesson) => `${lesson.sectionId}:${lesson.lineId}:${lesson.teacherId}:${lesson.day}:${lesson.lesson}`;
    const repairedKeys = new Set(repaired.map(key));
    const changed = lessons.filter((lesson) => !repairedKeys.has(key(lesson)));
    // Only the conflicting lessons moved, plus at most the other lessons of the same sections on that same day (a day closes up with no gap).
    const affectedDays = new Set(lessons.filter((lesson) => lesson.teacherId === teacherId && lesson.day === day).map((lesson) => `${lesson.sectionId}:${lesson.day}`));
    expect(changed.every((lesson) => affectedDays.has(`${lesson.sectionId}:${lesson.day}`))).toBe(true);
    expect(changed.length).toBeLessThan(lessons.length / 2);

    // The first version is exactly as it was, and still reports the conflicts against today's data.
    const original = await lessonsOf(page, version.id);
    expect(original).toEqual(lessons);
  } finally {
    await server.stop();
  }
});

test("(MF11) a subject given to another teacher: «استبدال المعلم في الجدول» swaps the teacher in the same slots", async ({ browser, page }, testInfo) => {
  test.setTimeout(240_000);
  await server.start(browser, "mf-currentdata-replace");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Current-Owner-2");
    const yearId = await seedReadySchool(page, server.baseUrl);
    const version = await generate(page, yearId);
    const lessons = await lessonsOf(page, version.id);

    // A new teacher takes the first section's first subject (nobody else is touched).
    const section = lessons[0].sectionId;
    const subject = lessons[0].subjectId;
    const subjects = await api<{ items: { id: number }[] }>(page, server.baseUrl, "GET", "/subjects/");
    expect(subjects.items.some((item) => item.id === subject)).toBe(true);
    const created = await api<{ id: number }>(page, server.baseUrl, "POST", "/teachers/", {
      fullName: "حيدر نبيل صالح", shortName: "حيدر", offDays: [], blockedPeriods: [], fullyReleased: false, releaseReason: null, releaseFrom: null, releaseTo: null,
      maxLessonsPerDay: null, maxLessonsPerWeek: null, notes: null, version: 0, specializationIds: [subject],
    });
    const stageId = (await api<{ stages: { id: number }[] }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/curriculum`)).stages[0].id;
    const matrix = await api<{ stage: { sections: { sectionId: number; cells: { entryId: number; subjectId?: number; assignmentId: number; version: number }[] }[] } }>(
      page, server.baseUrl, "GET", `/academic-years/${yearId}/workload/matrix?stageId=${stageId}`);
    const cell = matrix.stage.sections.find((row) => row.sectionId === section)!.cells.find((item) => item.subjectId === undefined || item.subjectId === subject) ?? matrix.stage.sections.find((row) => row.sectionId === section)!.cells[0];
    await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/workload/cell`, { sectionId: section, entryId: cell.entryId, teacherId: created.id, assignmentId: cell.assignmentId, version: cell.version });

    await page.goto(`${server.baseUrl}/timetable/view?version=${version.id}`);
    const panel = page.locator(".current-check");
    await expect(panel.locator(".current-findings li").first()).toContainText("أصبحت للمعلم");
    await expect(panel.locator(".current-findings li").first()).toContainText("والجدول ما زال باسم");
    await expect(page.getByRole("button", { name: timetable.approve })).toBeDisabled();
    await expect(panel.getByRole("button", { name: check.replace })).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath("mf11-reassigned.png"), fullPage: true });

    await panel.getByRole("button", { name: check.replace }).click();
    await expect(page.getByRole("status").filter({ hasText: check.replaced })).toBeVisible();
    await expect(page.locator(".current-check").getByText(check.status.fits)).toBeVisible();
    await expect(page.locator("#timetable-version option:checked")).toContainText(timetable.sources.teacherReplaced);
    const versions = await api<Summary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`);
    expect(versions).toHaveLength(2);
    const replaced = await lessonsOf(page, versions[0].id);
    // Only the lessons of the reassigned subject changed teacher, each in exactly its old slot; everything else is identical.
    const given = replaced.filter((lesson) => lesson.teacherId === created.id);
    expect(given.length).toBeGreaterThan(0);
    for (const lesson of given) {
      expect(lessons.some((old) => old.sectionId === lesson.sectionId && old.lineId === lesson.lineId && old.day === lesson.day && old.lesson === lesson.lesson && old.teacherId !== created.id)).toBe(true);
    }
    expect(replaced.filter((lesson) => lesson.teacherId !== created.id).every((lesson) => lessons.some((old) => JSON.stringify(old) === JSON.stringify(lesson)))).toBe(true);
    expect(replaced).toHaveLength(lessons.length);
    expect(await lessonsOf(page, version.id)).toEqual(lessons);
  } finally {
    await server.stop();
  }
});
