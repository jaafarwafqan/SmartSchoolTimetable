import { expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { messages } from "../src/i18n/messages";
import { repositoryRoot } from "./helpers";

// Shared by the chapters of the guide capture. The server (the published program on a temporary database) is started once by
// tools/Build-UserGuide.ps1 and its address is passed in GUIDE_BASE_URL.
export const baseUrl = process.env.GUIDE_BASE_URL ?? "";
if (baseUrl === "") throw new Error("GUIDE_BASE_URL is required: run tools/Build-UserGuide.ps1 (or start the program on a temporary database and set it).");

export const data = JSON.parse(readFileSync(join(repositoryRoot, "tools", "guide", "fictional-school.json"), "utf8")) as {
  schoolName: string; principal: string; owner: { username: string; password: string }; classTeachers: string[]; extraTeachers: { name: string; note: string }[]; sectionsPerGrade: number;
};
export const school = messages.school;
export const app = messages.app;
export const fixedTime = new Date("2026-10-05T09:00:00+03:00");

/** Signs in with the guide's owner (the first chapter creates the account). */
export async function signIn(page: Page): Promise<void> {
  await page.goto(baseUrl);
  const loginTitle = page.getByRole("heading", { name: app.loginTitle });
  const home = page.locator(".app-topbar");
  await expect(loginTitle.or(home)).toBeVisible({ timeout: 30_000 });
  if (await loginTitle.isVisible()) {
    await page.getByLabel(app.username).fill(data.owner.username);
    await page.getByLabel(app.password, { exact: true }).fill(data.owner.password);
    await page.getByRole("button", { name: app.loginAction }).click();
    await expect(home).toBeVisible({ timeout: 30_000 });
  }
}

export type Lesson = { sectionId: number; lineId: number; subjectId: number; teacherId: number; day: number; lesson: number };
export type Version = { id: number; version: number; status: string };

export async function generateTimetable(page: Page, afterStart?: () => Promise<void>): Promise<void> {
  await page.goto(`${baseUrl}/timetable/generate`);
  await expect(page.getByText(school.generation.readinessReady)).toBeVisible();
  await page.getByLabel(school.generation.timeLimit).selectOption("10");
  await page.getByRole("button", { name: school.generation.start }).click();
  if (afterStart) await afterStart();
  await expect(page.getByRole("heading", { name: school.generation.resultTitle })).toBeVisible({ timeout: 120_000 });
}

export async function openLatestVersion(page: Page): Promise<void> {
  await page.goto(`${baseUrl}/timetable/view`);
  await expect(page.getByRole("heading", { name: school.timetable.title, level: 1 })).toBeVisible();
  await expect(page.locator(".timetable-main .ui-tt-grid").first()).toBeVisible({ timeout: 30_000 });
}
