import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { api } from "./support/api";
import { seedReadySchool } from "./support/readySchool";
import { expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

const server = new ApiServer();
const dashboard = messages.school.dashboard;

type Summary = { id: number; version: number; status: string };
type Lesson = { teacherId: number; day: number };
type Teacher = { id: number; fullName: string; shortName: string; version: number; blockedPeriods: unknown[]; specializationIds?: number[] };

async function generate(page: Page, yearId: number): Promise<Summary> {
  const run = await api<{ id: number }>(page, server.baseUrl, "POST", `/academic-years/${yearId}/generation/runs`, { timeLimitSeconds: 10, deterministic: true, seed: 3 });
  for (let attempt = 0; attempt < 200; attempt++) {
    const state = await api<{ status: string }>(page, server.baseUrl, "GET", `/generation/runs/${run.id}`);
    if (state.status === "completed") break;
    await page.waitForTimeout(500);
  }
  return (await api<Summary[]>(page, server.baseUrl, "GET", `/academic-years/${yearId}/timetables`))[0];
}

test("(M4b) the dashboard warns when the approved timetable conflicts with today's data, and the removed numbers are gone", async ({ browser, page }) => {
  test.setTimeout(240_000);
  await server.start(browser, "m4b-dashboard");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Dashboard-Owner-1");
    const yearId = await seedReadySchool(page, server.baseUrl);
    const version = await generate(page, yearId);
    await api(page, server.baseUrl, "POST", `/timetables/${version.id}/approve`, { version: version.version });

    // Approved and still fitting: no warning; the removed numbers are gone.
    await page.goto(`${server.baseUrl}/`);
    await expect(page.getByRole("heading", { name: dashboard.countsTitle })).toBeVisible();
    await expect(page.getByText("الجدول المعتمد يتعارض")).toHaveCount(0);
    await expect(page.locator(".count-item").filter({ hasText: dashboard.counts.teachers })).toBeVisible();
    await expect(page.locator(".count-item")).toHaveCount(4);

    // A teacher becomes unavailable on his busiest day: the approved timetable now conflicts.
    const lessons = (await api<{ lessons: Lesson[] }>(page, server.baseUrl, "GET", `/timetables/${version.id}`)).lessons;
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
    const alert = page.getByRole("status").filter({ hasText: "الجدول المعتمد يتعارض مع بيانات المدرسة الحالية" });
    await expect(alert).toBeVisible();
    await expectNoSeriousA11yViolations(page, "dashboard with an approved-version alert");
    await alert.getByRole("link", { name: dashboard.openApproved }).click();
    await expect(page).toHaveURL(/\/timetable\/view/);
  } finally {
    await server.stop();
  }
});
