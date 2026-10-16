import { expect, test } from "@playwright/test";
import { isolate } from "../src/i18n/isolate";
import { messages } from "../src/i18n/messages";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

// The owner's generation rules (ADR 0053) on a fictional school: the daily limit of a subject for one section, the school's
// «أقصى حصص متتالية للمعلم», the readiness error it causes and its fix, and the teacher's own limit in his card.
const server = new ApiServer();
const readiness = messages.school.readiness;
const profile = messages.school.schedulingProfile;
const teachers = messages.school.teachers;

type Subject = { id: number; capOverrides?: Array<{ scope: string; scopeId: number; cap: number }> };
type Teacher = { id: number; maxConsecutive?: number | null };
type Curriculum = { stages: Array<{ id: number }> };
type Paged<T> = { items: T[] };
type Section = { id: number; label: string };

test("(generation rules) a section's daily limit, the school's consecutive limit with its readiness error, and a teacher's own limit", async ({ browser, page }) => {
  test.setTimeout(240_000);
  await server.start(browser, "phase5-generation-rules");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Rules-Owner-1");
    const { yearId } = await prepareSchool(page, server.baseUrl, {
      schoolType: "intermediate", shiftMode: "morning", lessons: 6, grades: [{ gradeKey: "intermediate-1", sections: 2 }],
    });
    const physics = await api<Subject>(page, server.baseUrl, "POST", "/subjects/", {
      name: "الفيزياء", colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false,
      requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
    });
    const curriculum = await api<Curriculum>(page, server.baseUrl, "GET", `/academic-years/${yearId}/curriculum`);
    const stageId = curriculum.stages[0].id;
    // 12 + 12 lessons for one teacher (a curriculum line holds at most 15).
    const chemistry = await api<Subject>(page, server.baseUrl, "POST", "/subjects/", {
      name: "الكيمياء", colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false,
      requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
    });
    for (const subject of [physics, chemistry])
      await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/curriculum/cell`, { stageId, subjectId: subject.id, label: null, weeklyLessons: 12, entryId: null, version: null });
    const teacher = await api<Teacher>(page, server.baseUrl, "POST", "/teachers/", {
      fullName: "هدى سالم", shortName: "هدى", offDays: [], blockedPeriods: [], fullyReleased: false,
      releaseReason: null, releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null,
      notes: null, version: 0, specializationIds: [physics.id, chemistry.id],
    });
    const sections = (await api<Paged<Section>>(page, server.baseUrl, "GET", `/academic-years/${yearId}/stages/${stageId}/sections?pageSize=100`)).items;
    await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/workload/bulk/class-teacher`, { teacherId: teacher.id, sectionId: sections[0].id, entryIds: [], overwrite: false });

    // 1. The daily limit of the subject for the second section only: «حسب الصف والشعبة», then one lesson a day.
    await page.goto(`${server.baseUrl}/settings/advanced`);
    await expect(page.getByRole("heading", { name: profile.dailyCap.title })).toBeVisible();
    await page.getByRole("row").filter({ hasText: "الفيزياء" }).getByRole("button", { name: profile.dailyCap.details }).click();
    await expect(page.getByRole("heading", { name: profile.dailyCap.detailsTitle(isolate("الفيزياء")) })).toBeVisible();
    const sectionSelect = page.getByRole("combobox", { name: profile.dailyCap.chooseForSection("الفيزياء", `${(await stageName(page, yearId, stageId))} / ${sections[1].label}`) });
    await sectionSelect.selectOption({ label: profile.dailyCap.oneLesson });
    await expect(page.getByRole("status").filter({ hasText: profile.dailyCap.saved })).toBeVisible();
    const saved = (await api<Paged<Subject>>(page, server.baseUrl, "GET", "/subjects/?pageSize=100")).items.find((item) => item.id === physics.id)!;
    expect(saved.capOverrides).toEqual([{ scope: "section", scopeId: sections[1].id, cap: 1 }]);
    await expectNoSeriousA11yViolations(page, "daily limit per section");
    await page.setViewportSize({ width: 375, height: 800 });
    await expectNoPageScrollX(page, "daily limit at 375px");
    await page.setViewportSize({ width: 1280, height: 900 });

    // 2. The school's limit of two lessons in a row: 24 lessons do not fit in 5 days × 4.
    await page.getByLabel(profile.consecutive.label).selectOption("2");
    await page.getByRole("button", { name: profile.save }).click();
    await expect(page.getByRole("status").filter({ hasText: profile.saved })).toBeVisible();
    await page.goto(`${server.baseUrl}/readiness`);
    await expect(page.getByText(readiness.consecutiveAdviceRaise)).toBeVisible();
    await expect(page.getByText("هدى سالم").first()).toBeVisible();
    await expectNoSeriousA11yViolations(page, "consecutive readiness");

    // 3. The fix: the teacher's own limit of three in his card; the error goes away.
    await page.getByRole("link", { name: readiness.fixes.raiseTeacherConsecutive }).first().click();
    await page.locator(".ui-expandable-toggle", { hasText: "هدى سالم" }).click();
    const row = page.locator(".ui-expandable.is-expanded");
    await row.getByLabel(teachers.consecutive).selectOption("3");
    await row.getByRole("button", { name: teachers.save }).click();
    await expect.poll(async () => (await api<Paged<Teacher>>(page, server.baseUrl, "GET", "/teachers/?pageSize=100")).items.find((item) => item.id === teacher.id)?.maxConsecutive).toBe(3);
    await page.goto(`${server.baseUrl}/readiness`);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(page.getByText(readiness.consecutiveAdviceRaise)).toHaveCount(0);
  } finally {
    await server.stop();
  }
});

async function stageName(page: import("@playwright/test").Page, yearId: number, stageId: number): Promise<string> {
  const stages = await api<Paged<{ id: number; name: string }>>(page, server.baseUrl, "GET", `/academic-years/${yearId}/stages/?pageSize=100`);
  return stages.items.find((stage) => stage.id === stageId)!.name;
}
