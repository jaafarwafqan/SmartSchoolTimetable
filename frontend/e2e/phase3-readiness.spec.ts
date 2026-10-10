import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { isolate } from "../src/i18n/isolate";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { breakpoints, expectBreakpointScreenshots, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, setupOwner } from "./support/flows";

const server = new ApiServer();
const readiness = messages.school.readiness;

type Subject = { id: number };
type Teacher = { id: number };
type Curriculum = { stages: Array<{ id: number }> };

async function seedUnassigned(page: Page) {
  const { yearId } = await prepareSchool(page, server.baseUrl, {
    schoolType: "intermediate", shiftMode: "morning", grades: [{ gradeKey: "intermediate-1", sections: 1 }],
  });
  const subject = await api<Subject>(page, server.baseUrl, "POST", "/subjects/", {
    name: "الرياضيات", colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false,
    requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
  });
  const curriculum = await api<Curriculum>(page, server.baseUrl, "GET", `/academic-years/${yearId}/curriculum`);
  await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/curriculum/cell`, {
    stageId: curriculum.stages[0].id, subjectId: subject.id, label: null, weeklyLessons: 5, entryId: null, version: null,
  });
}

test("(3D) dashboard and readiness report show the real unassigned-line finding", async ({ browser, page }) => {
  await server.start(browser, "phase3-readiness");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Readiness-Owner-1");
    await seedUnassigned(page);
    // The data was written through the API behind the open dashboard: reload so the card reads it (no race).
    await page.reload();
    const card = page.locator(".readiness-card");
    await expect(card.getByText(readiness.blocked)).toBeVisible();
    await card.getByRole("link").click();
    await expect(page.getByRole("heading", { name: readiness.title, level: 1 })).toBeVisible();
    await expect(page.getByText(readiness.messages.UNASSIGNED_LINES("بند واحد", isolate("الرياضيات")))).toBeVisible();
    await expect(page.getByText(readiness.messages.SECTION_UNDER_CAPACITY(isolate("الأول المتوسط / أ"), "٥ حصص", "٣٥ حصة"))).toBeVisible();
    await expectNoSeriousA11yViolations(page, "readiness report");
    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `readiness report at ${width}px`);
      await expectNoTextOverlap(page.locator("main"), `readiness report at ${width}px`);
    }
    await page.setViewportSize({ width: 1280, height: 900 });
    await expectBreakpointScreenshots(page, "phase3-readiness", [page.locator(".readiness-meta")]); // run time and hash change
  } finally {
    await server.stop();
  }
});

test("(3D) exact 28/25 teacher shortage opens workload and is fixed from the deep link", async ({ browser, page }) => {
  await server.start(browser, "phase3-readiness-shortage");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Readiness-Shortage-1");
    const { yearId } = await prepareSchool(page, server.baseUrl, {
      schoolType: "intermediate", shiftMode: "morning", grades: [{ gradeKey: "intermediate-1", sections: 1 }],
    });
    const subject = async (name: string) => api<Subject>(page, server.baseUrl, "POST", "/subjects/", {
      name, colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false,
      requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
    });
    const physics = await subject("الفيزياء");
    const maths = await subject("الرياضيات");
    const curriculum = await api<Curriculum>(page, server.baseUrl, "GET", `/academic-years/${yearId}/curriculum`);
    const stageId = curriculum.stages[0].id;
    for (const [subjectId, label, lessons] of [[physics.id, "نظري", 15], [maths.id, "تطبيقي", 13]] as const) {
      await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/curriculum/cell`, {
        stageId, subjectId, label, weeklyLessons: lessons, entryId: null, version: null,
      });
    }
    const ahmed = await api<Teacher>(page, server.baseUrl, "POST", "/teachers/", {
      fullName: "أحمد علي حسن", shortName: "أحمد", offDays: [], blockedPeriods: [], fullyReleased: false,
      releaseReason: null, releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: 25,
      notes: null, version: 0, specializationIds: [physics.id, maths.id],
    });
    const saad = await api<Teacher>(page, server.baseUrl, "POST", "/teachers/", {
      fullName: "سعد كاظم حسن", shortName: "سعد", offDays: [], blockedPeriods: [], fullyReleased: false,
      releaseReason: null, releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null,
      notes: null, version: 0, specializationIds: [physics.id],
    });
    const matrix = await api<{ stage: { sections: Array<{ sectionId: number }> } }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/workload/matrix?stageId=${stageId}`);
    await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/workload/bulk/class-teacher`, {
      teacherId: ahmed.id, sectionId: matrix.stage.sections[0].sectionId, entryIds: [], overwrite: false,
    });

    await page.goto(`${server.baseUrl}/readiness`);
    await expect(page.getByText(readiness.messages.TEACHER_OVERLOAD(
      isolate("أحمد علي حسن"), "٢٨ حصة", "٢٥", "٣ حصص",
    ))).toBeVisible();
    await page.getByRole("link", { name: readiness.fixes.moveWorkload }).click();
    const workload = messages.school.workload;
    await expect(page.getByRole("heading", { name: workload.title, level: 1 })).toBeVisible();
    await page.getByRole("combobox", { name: workload.cellLabel("الفيزياء - نظري", "أ") }).selectOption(String(saad.id));
    await expect(page.getByRole("status").filter({ hasText: workload.saved })).toBeVisible();
    await page.goto(`${server.baseUrl}/readiness`);
    await expect(page.getByText(readiness.ready)).toBeVisible();
    await expect(page.getByText(readiness.messages.TEACHER_OVERLOAD(
      isolate("أحمد علي حسن"), "٢٨ حصة", "٢٥", "٣ حصص",
    ))).toHaveCount(0);
    await expectNoSeriousA11yViolations(page, "fixed teacher shortage readiness");
  } finally {
    await server.stop();
  }
});