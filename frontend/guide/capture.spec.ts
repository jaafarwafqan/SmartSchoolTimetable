import { expect, test } from "@playwright/test";
import { join } from "node:path";
import { api } from "../e2e/support/api";
import { repositoryRoot, shot } from "./helpers";
import { baseUrl, data as fictional, fixedTime, generateTimetable, openLatestVersion, school, signIn, type Lesson, type Version } from "./shared";

// The user-guide scenario, part 2 (chapters 6 to 12): readiness, generating, reading the result, manual edits, versions and the check against the
// school's data, printing and the settings screens. Part 1 (the first run and the wizard) is 01-first-run.spec.ts; they run in that order.
test.describe.configure({ mode: "serial" });

test("ch6, ch7 — readiness and generating the timetable", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  await page.goto(`${baseUrl}/readiness`);
  await expect(page.getByText(school.readiness.ready, { exact: true })).toBeVisible();
  await shot(page, "40-readiness", { part: "top" });
  await page.goto(`${baseUrl}/timetable/generate`);
  await expect(page.getByText(school.generation.readinessReady)).toBeVisible();
  await shot(page, "41-generate-start");
  await generateTimetable(page, async () => {
    await page.waitForTimeout(1500);
    await shot(page, "42-generate-progress");
  });
  await shot(page, "43-generate-result", { target: ".page-card:has(#generation-result-title)" });
  await page.getByText(school.generation.plain.technical).first().click();
  await shot(page, "44-generate-technical", { target: ".page-card:has(#generation-result-title)" });
});

test("ch8 — reading the timetable: by section, by teacher, the whole school, the semester", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  await openLatestVersion(page);
  await shot(page, "45-view-section", { width: 1600, target: ".timetable-main" });
  await page.locator("#timetable-view").selectOption("teacher");
  await shot(page, "46-view-teacher", { width: 1600, target: ".timetable-main" });
  await page.locator("#timetable-view").selectOption("master");
  await shot(page, "47-view-master", { width: 1600, target: ".timetable-main" });
  await page.locator("#timetable-view").selectOption("stage");
  await shot(page, "47b-view-stage", { width: 1600, target: ".timetable-main" });
  await page.locator("#timetable-view").selectOption("section");
  await page.getByRole("group", { name: school.timetable.termLabel }).getByRole("button", { name: school.timetable.terms[2] }).click();
  await shot(page, "48-view-semester-two", { width: 1600, target: ".timetable-main" });
});

test("ch9 — manual edit, undo and redo, keep or discard before regenerating", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  const year = (await api<{ items: { id: number }[] }>(page, baseUrl, "GET", "/academic-years/")).items[0];
  const [latest] = await api<Version[]>(page, baseUrl, "GET", `/academic-years/${year.id}/timetables`);
  const data = await api<{ days: number[]; sections: { id: number }[]; lessons: Lesson[] }>(page, baseUrl, "GET", `/timetables/${latest.id}`);
  // A swap of two lessons of the first section that the program's own checker accepts.
  const { moveOrSwap } = await import("../src/features/timetable/editing");
  const section = data.sections[0];
  const lessons = data.lessons.filter((lesson) => lesson.sectionId === section.id);
  let swap: [Lesson, Lesson] | null = null;
  for (const first of lessons) {
    for (const second of lessons) {
      if (swap || first.lineId === second.lineId || first.day * 100 + first.lesson >= second.day * 100 + second.lesson) continue;
      const edited = moveOrSwap(data.lessons, { sectionId: section.id, day: first.day, lesson: first.lesson }, { sectionId: section.id, day: second.day, lesson: second.lesson });
      const check = await api<{ violations: unknown[] }>(page, baseUrl, "POST", `/timetables/${latest.id}/check`, { lessons: edited, note: null });
      if (check.violations.length === 0) swap = [first, second];
    }
  }
  expect(swap).not.toBeNull();
  await openLatestVersion(page);
  await page.getByRole("button", { name: school.timetable.edit }).click();
  await shot(page, "50-edit-mode", { width: 1600, target: ".timetable-main" });
  const editor = page.locator(".timetable-editor .ui-tt-grid");
  const cell = (lesson: Lesson) => editor.locator(`[data-cell="${data.days.indexOf(lesson.day)}:${lesson.lesson - 1}"] .ui-tt-cell`);
  await cell(swap![0]).click();
  await shot(page, "51-edit-first-cell", { width: 1600, target: ".timetable-main" });
  await cell(swap![1]).click();
  await expect(page.getByText(school.timetable.noConflicts)).toBeVisible();
  await shot(page, "52-edit-swapped", { width: 1600, target: ".timetable-main" });
  await page.getByRole("button", { name: school.timetable.undo }).click();
  await shot(page, "53-edit-undo", { width: 1600, target: ".timetable-main" });
  await page.getByRole("button", { name: school.timetable.redo }).click();
  // The editor works from every view: the same edit, seen in the teacher view of the moved lesson's teacher.
  await page.locator("#timetable-view").selectOption("teacher");
  await page.locator("#timetable-teacher").selectOption(String(swap![0].teacherId));
  await expect(page.getByText(school.timetable.noConflicts)).toBeVisible();
  await shot(page, "53b-edit-teacher-view", { width: 1600, target: ".timetable-main" });
  await page.getByRole("button", { name: school.timetable.saveEdit }).click();
  await expect(page.getByText(school.timetable.savedEdit)).toBeVisible();
  await expect(page.locator(".timetable-main .ui-tt-grid").first()).toBeVisible({ timeout: 30_000 });
  await shot(page, "54-edit-saved", { width: 1600, part: "top" });
  // Regenerating after a manual edit asks what to do with the moved lessons.
  await page.goto(`${baseUrl}/timetable/generate`);
  await expect(page.getByText(school.generation.readinessReady)).toBeVisible();
  await page.getByLabel(school.generation.timeLimit).selectOption("10");
  await page.getByRole("button", { name: school.generation.start }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await shot(page, "55-keep-or-discard", { window: true });
  await page.getByRole("radio", { name: school.lifecycle.keepEditsKeep }).check();
  await page.getByRole("dialog").getByRole("button", { name: school.lifecycle.keepEditsConfirm }).click();
  await expect(page.getByRole("heading", { name: school.generation.resultTitle })).toBeVisible({ timeout: 120_000 });
});

test("ch10 — versions: compare, approve, and the check against the data of today", async ({ page }) => {
  test.setTimeout(600_000);
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  await openLatestVersion(page);
  await shot(page, "56-versions", { width: 1600, part: "top" });
  await page.getByRole("button", { name: school.lifecycle.compare }).click();
  await expect(page.getByRole("heading", { name: school.lifecycle.compareTitle })).toBeVisible();
  await shot(page, "57-compare");
  await page.getByRole("button", { name: school.lifecycle.compareClose }).click();
  await page.getByRole("button", { name: school.timetable.approve }).click();
  await shot(page, "58-approve-dialog", { window: true });
  await page.getByRole("dialog").getByRole("button", { name: school.timetable.approve }).click();
  await expect(page.getByText(school.timetable.approvedDone)).toBeVisible();
  await shot(page, "59-approved", { width: 1600, part: "top" });

  // The data of the school changes after the approval: a teacher is away on a day. The saved timetable now conflicts, and says so.
  const year = (await api<{ items: { id: number }[] }>(page, baseUrl, "GET", "/academic-years/")).items[0];
  const versions = await api<Version[]>(page, baseUrl, "GET", `/academic-years/${year.id}/timetables`);
  const approved = versions.find((version) => version.status === "approved")!;
  const timetable = await api<{ lessons: Lesson[] }>(page, baseUrl, "GET", `/timetables/${approved.id}`);
  const counts = new Map<string, number>();
  for (const lesson of timetable.lessons) counts.set(`${lesson.teacherId}:${lesson.day}`, (counts.get(`${lesson.teacherId}:${lesson.day}`) ?? 0) + 1);
  const [busiest] = [...counts.entries()].sort((a, b) => b[1] - a[1]);
  const [teacherId, day] = busiest[0].split(":").map(Number);
  const teachers = await api<{ items: { id: number; fullName: string; shortName: string; version: number; blockedPeriods: unknown[]; specializationIds?: number[] }[] }>(page, baseUrl, "GET", "/teachers/?pageSize=100");
  const chosen = teachers.items.find((item) => item.id === teacherId)!;
  await api(page, baseUrl, "PUT", `/teachers/${teacherId}`, {
    fullName: chosen.fullName, shortName: chosen.shortName, offDays: [day], blockedPeriods: chosen.blockedPeriods, fullyReleased: false, releaseReason: null,
    releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null, notes: null, version: chosen.version, specializationIds: chosen.specializationIds ?? [],
  });
  await page.goto(`${baseUrl}/`);
  await expect(page.getByText(/الجدول المعتمد يتعارض/)).toBeVisible();
  await shot(page, "60-dashboard-conflict", { part: "top" });
  await openLatestVersion(page);
  await expect(page.locator(".current-check .current-findings li").first()).toBeVisible();
  await shot(page, "61-current-check", { width: 1600, target: ".timetable-main" });
});

test("ch11 — printing: the print panel, colour or black and white, a page per stage, the Excel export", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  await openLatestVersion(page);
  await page.getByText(school.printing.title).click();
  await expect(page.locator("#print-scope")).toBeVisible();
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(1500);
  await shot(page, "62-print-panel", { width: 1600, target: ".timetable-main" });
  const folder = join(repositoryRoot, "docs", "guide", "tmp");
  const pdf = async (name: string) => {
    await page.emulateMedia({ media: null });
    await page.pdf({ path: join(folder, `${name}.pdf`), preferCSSPageSize: true, printBackground: false });
  };
  await pdf("print-section-colour");
  await page.locator("#print-color").selectOption("bw");
  await pdf("print-section-bw");
  await page.locator("#print-color").selectOption("color");
  await page.locator("#print-scope").selectOption("teachers");
  await pdf("print-teachers");
  await page.locator("#print-scope").selectOption("school");
  await pdf("print-school-colour");
  await page.locator("#print-scope").selectOption("stages");
  await pdf("print-school-per-stage");
  await page.locator("#print-scope").selectOption("current");
  await expect(page.getByRole("link", { name: school.timetable.exportExcel })).toBeVisible();
  await shot(page, "63-print-options", { width: 1600, target: ".timetable-main" });
});

test("ch12 — sharing with phones: the sharing page, the codes, the key copy, the teacher cards and the staff-room screen", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  // Chapter 10 left a teacher away on a day (the approved version then conflicts): the guide puts the day back before sharing.
  const teachers = await api<{ items: { id: number; fullName: string; shortName: string; version: number; offDays: number[]; blockedPeriods: unknown[]; specializationIds?: number[] }[] }>(page, baseUrl, "GET", "/teachers/?pageSize=100");
  for (const teacher of teachers.items.filter((item) => item.offDays.length > 0)) {
    await api(page, baseUrl, "PUT", `/teachers/${teacher.id}`, {
      fullName: teacher.fullName, shortName: teacher.shortName, offDays: [], blockedPeriods: teacher.blockedPeriods, fullyReleased: false, releaseReason: null,
      releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null, notes: null, version: teacher.version, specializationIds: teacher.specializationIds ?? [],
    });
  }
  await page.goto(`${baseUrl}/timetable/share`);
  await expect(page.getByRole("heading", { name: school.sharing.title, level: 1 })).toBeVisible();
  await shot(page, "69a-share-page", { width: 1440, part: "top" });
  await page.getByRole("button", { name: school.sharing.pairing.show }).click();
  const backup = page.getByRole("dialog", { name: school.sharing.backupDialog.title });
  await expect(backup).toBeVisible();
  await shot(page, "69b-share-key-copy", { window: true });
  await backup.getByRole("button", { name: school.sharing.backupDialog.later }).click();
  await expect(page.getByRole("img", { name: school.sharing.pairing.alt })).toBeVisible();
  await shot(page, "69c-share-pairing", { target: ".settings-section:has(#share-pairing-title)" });
  await page.getByLabel(school.sharing.teacher.choose, { exact: true }).selectOption({ index: 1 });
  await page.getByRole("button", { name: school.sharing.teacher.show }).click();
  await expect(page.locator(".settings-section:has(#share-teacher-title) .share-qr")).toBeVisible();
  await shot(page, "69d-share-teacher", { target: ".settings-section:has(#share-teacher-title)" });
  // The teacher cards: printing is replaced by a PDF of the same document (rendered to a figure by render_prints.py).
  await page.evaluate(() => { window.print = () => undefined; });
  await page.getByRole("button", { name: school.sharing.cards.print }).click();
  await expect(page.locator(".cards-document .teacher-card").first()).toBeAttached();
  await page.pdf({ path: join(repositoryRoot, "docs", "guide", "tmp", "print-teacher-cards.pdf"), preferCSSPageSize: true, printBackground: true });
  await page.evaluate(() => window.dispatchEvent(new Event("afterprint")));
  // The staff-room screen with Monday's lessons of the approved version at 09:00 (the screen's data is built from the same version).
  const year = (await api<{ items: { id: number }[] }>(page, baseUrl, "GET", "/academic-years/")).items[0];
  const approved = (await api<Version[]>(page, baseUrl, "GET", `/academic-years/${year.id}/timetables`)).find((version) => version.status === "approved")!;
  const grid = await api<{ shifts: { lessons: { number: number; startMinute: number; endMinute: number }[] }[]; sections: { id: number; stageName: string; label: string }[];
    subjects: { id: number; name: string; colorIndex: number }[]; teachers: { id: number; shortName: string }[]; lessons: Lesson[]; summary: { number: number } }>(page, baseUrl, "GET", `/timetables/${approved.id}`);
  const sections = grid.sections.slice(0, 8);
  const kioskDay = {
    schoolName: fictional.schoolName, date: "2026-10-05", timeZone: "Asia/Baghdad", status: "school", dayOffTitle: null, versionNumber: grid.summary.number, semester: 1, session: 1,
    periods: grid.shifts[0].lessons.map((lesson) => ({ number: lesson.number, startMinute: lesson.startMinute, endMinute: lesson.endMinute })),
    sections: sections.map((section, index) => ({ index, stageName: section.stageName, label: section.label })),
    lessons: grid.lessons.filter((lesson) => lesson.day === 1 && sections.some((section) => section.id === lesson.sectionId)).map((lesson) => {
      const subject = grid.subjects.find((item) => item.id === lesson.subjectId)!;
      return { section: sections.findIndex((section) => section.id === lesson.sectionId), lesson: lesson.lesson, subject: subject.name, colorIndex: subject.colorIndex,
        teacher: grid.teachers.find((item) => item.id === lesson.teacherId)?.shortName ?? "" };
    }),
  };
  await page.route("**/api/v1/kiosk/today", (route) => route.fulfill({ json: kioskDay }));
  await page.setViewportSize({ width: 1600, height: 900 });
  await page.goto(`${baseUrl}/kiosk#k=guideguideguideguideguideguide12`);
  await expect(page.getByRole("table")).toBeVisible();
  await shot(page, "69f-kiosk", { window: true });
});

test("ch13 — settings: general, account and security, backups, advanced, history, about", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  await page.goto(`${baseUrl}/settings/general`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await shot(page, "70-settings-general");
  await page.goto(`${baseUrl}/settings/account`);
  await shot(page, "71-settings-account");
  await page.goto(`${baseUrl}/settings/backup`);
  await expect(page.getByRole("button", { name: school.backup.create })).toBeVisible();
  await page.getByRole("button", { name: school.backup.create }).click();
  await expect(page.getByRole("status").filter({ hasText: /timetable-backup-/ })).toBeVisible();
  await shot(page, "72-settings-backup");
  await page.goto(`${baseUrl}/settings/advanced`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(page.getByRole("heading", { name: school.schedulingProfile.title })).toBeVisible();
  await expect(page.getByRole("heading", { name: school.schedulingProfile.dailyCap.title })).toBeVisible();
  await page.waitForLoadState("networkidle");
  await shot(page, "73-settings-advanced");
  await page.goto(`${baseUrl}/settings/history`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await page.waitForLoadState("networkidle");
  await expect(page.getByText(/نسخة احتياطية/).first()).toBeVisible();
  await page.waitForTimeout(800);
  await shot(page, "74-settings-history");
  await page.goto(`${baseUrl}/about`);
  await expect(page.getByRole("heading", { name: school.about.title, level: 1 })).toBeVisible();
  await shot(page, "75-settings-about");
});
