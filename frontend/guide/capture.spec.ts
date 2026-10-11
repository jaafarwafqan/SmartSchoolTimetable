import { expect, test } from "@playwright/test";
import { join } from "node:path";
import { api } from "../e2e/support/api";
import { repositoryRoot, shot } from "./helpers";
import { baseUrl, fixedTime, generateTimetable, openLatestVersion, school, signIn, type Lesson, type Version } from "./shared";

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
  await shot(page, "47-view-master", { width: 1900, target: ".timetable-main" });
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

test("ch12 — settings: general, account and security, backups, advanced, history, about", async ({ page }) => {
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
  await shot(page, "73-settings-advanced");
  await page.goto(`${baseUrl}/settings/history`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await page.waitForLoadState("networkidle");
  await expect(page.getByText(/نسخة احتياطية/).first()).toBeVisible();
  await page.waitForTimeout(800);
  await shot(page, "74-settings-history");
  await page.goto(`${baseUrl}/settings/about`);
  await expect(page.getByRole("heading", { name: school.about.title, level: 1 })).toBeVisible();
  await shot(page, "75-settings-about");
});
