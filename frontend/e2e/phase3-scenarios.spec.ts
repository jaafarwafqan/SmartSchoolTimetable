import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { isolate } from "../src/i18n/isolate";
import { formatNumber } from "../src/lib/format";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import {
  breakpoints, expectBreakpointScreenshots, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, goToSection, setupOwner,
} from "./support/flows";
import { UxMeter } from "./support/ux";

// Phase 3E scenarios on fresh temporary databases (owner's Phase 3 finish D1, D4):
// (a) a 12-section primary school assigned with the class-teacher bulk action, then readiness is ready;
// (h) a 12-section intermediate school assigned by the suggester (preview = applied, existing kept);
// (c) a resource shortage; (d) protection of subject, section and stage; (f) orphan blocked periods.
// (b) 28/25, (g) readiness + dashboard are in phase3-readiness.spec.ts; (e) undo in phase3-workload.spec.ts.
const school = messages.school;
const workload = school.workload;
const readiness = school.readiness;
const arab = (value: number) => formatNumber(value, "arab");

test.describe.configure({ mode: "serial" });

type Subject = { id: number; name: string };
type Paged<T> = { items: T[] };
type Teacher = { id: number };

async function subjects(page: Page, baseUrl: string): Promise<Map<string, number>> {
  const list = await api<Paged<Subject>>(page, baseUrl, "GET", "/subjects/?pageSize=100");
  return new Map(list.items.map((subject) => [subject.name, subject.id]));
}

async function teacher(page: Page, baseUrl: string, fullName: string, specializationIds: number[] = [], maxLessonsPerWeek: number | null = null) {
  return api<Teacher>(page, baseUrl, "POST", "/teachers/", {
    fullName, shortName: null, offDays: [], blockedPeriods: [], fullyReleased: false, releaseReason: null, releaseFrom: null, releaseTo: null,
    maxLessonsPerDay: null, maxLessonsPerWeek, notes: null, version: 0, specializationIds,
  });
}

async function expectLayout(page: Page, screen: string) {
  await expectNoSeriousA11yViolations(page, screen);
  for (const width of breakpoints) {
    await page.setViewportSize({ width, height: 900 });
    await expectNoPageScrollX(page, `${screen} at ${width}px`);
    await expectNoTextOverlap(page.locator("main"), `${screen} at ${width}px`);
  }
  await page.setViewportSize({ width: 1280, height: 900 });
}

const classTeachers = ["أمل حسين علي", "بشرى كاظم جواد", "رنا عادل مهدي", "سلوى فاضل حسن", "شيماء جاسم محمد", "ضحى رعد كريم",
  "علياء ستار جبار", "غدير ناصر علوان", "فرح صادق حسون", "لمى عباس حميد", "منى وليد سعيد", "نادية ماجد عزيز"];

test("(a) primary: twelve class teachers assigned with the bulk action, then readiness is ready", async ({ browser, page }) => {
  const server = new ApiServer();
  await server.start(browser, "phase3-primary");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Primary-Owner-1");
    const { yearId } = await prepareSchool(page, server.baseUrl, {
      schoolType: "primary", shiftMode: "morning", grades: [{ gradeKey: "primary-1", sections: 6 }, { gradeKey: "primary-2", sections: 6 }],
    });
    await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/curriculum/suggested`, { optionalSubjects: [], confirm: true });
    for (const name of classTeachers) await teacher(page, server.baseUrl, name);

    await goToSection(page, school.nav.workload);
    const ux = new UxMeter("phase3 primary 12 sections");
    await ux.act(page.getByText(workload.bulk.classTeacher, { exact: true }));
    const tool = page.locator("details", { hasText: workload.bulk.classTeacher });
    for (const [index, name] of classTeachers.entries()) {
      const grade = index < 6 ? "الأول الابتدائي" : "الثاني الابتدائي";
      const section = ["أ", "ب", "ج", "د", "هـ", "و"][index % 6];
      await ux.select(tool.getByLabel(workload.bulk.teacher, { exact: true }), { label: name });
      if (index === 0 || index === 6) await ux.select(tool.getByLabel(workload.stage, { exact: true }), { label: grade });
      await ux.select(tool.getByLabel(workload.bulk.sectionChoice, { exact: true }), { label: section });
      await ux.act(tool.getByRole("button", { name: workload.bulk.preview }));
      await ux.act(tool.getByRole("button", { name: workload.bulk.apply }));
      await ux.act(page.getByRole("dialog", { name: workload.bulk.confirmTitle }).getByRole("button", { name: workload.bulk.apply }));
      await expect(tool.getByRole("status")).toBeVisible();
    }
    console.log(ux.report());
    expect(ux.typed).toBe(0);
    await expect(page.locator(".workload-flag", { hasText: workload.unassigned })).toHaveCount(0);

    await page.goto(`${server.baseUrl}/readiness`);
    await expect(page.getByText(readiness.ready, { exact: true })).toBeVisible();
    await expect(page.getByText(readiness.noErrors)).toBeVisible();
    await expectLayout(page, "primary readiness");
  } finally {
    await server.stop();
  }
});

test("(h) intermediate: the suggester assigns twelve sections; its preview equals the result and keeps existing assignments", async ({ browser, page }) => {
  const server = new ApiServer();
  await server.start(browser, "phase3-intermediate");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Intermediate-Owner-1");
    const { yearId } = await prepareSchool(page, server.baseUrl, {
      schoolType: "intermediate", shiftMode: "morning",
      grades: ["intermediate-1", "intermediate-2", "intermediate-3"].map((gradeKey) => ({ gradeKey, sections: 4 })),
    });
    await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/curriculum/suggested`, { optionalSubjects: [], confirm: true });
    const ids = await subjects(page, server.baseUrl);
    const of = (...names: string[]) => names.map((name) => ids.get(name)!);
    const specialists: Array<[string, number[]]> = [
      ["أحمد علي حسن", of("اللغة العربية")], ["زينب جاسم محمد", of("اللغة العربية")],
      ["حيدر عبد الأمير", of("اللغة الإنكليزية")], ["هدى ناصر علوان", of("اللغة الإنكليزية")],
      ["يوسف رعد حمزة", of("الرياضيات")], ["رقية ستار جبار", of("الرياضيات")],
      ["مصطفى كريم عباس", of("الكيمياء", "الفيزياء", "الأحياء")], ["آمنة كاظم عبيد", of("الكيمياء", "الفيزياء", "الأحياء")],
      ["كرار فلاح مهدي", of("الكيمياء", "الفيزياء", "الأحياء")],
      ["محمد جواد كاظم", of("الاجتماعيات", "التربية الإسلامية")], ["سارة عادل حسن", of("الاجتماعيات", "التربية الإسلامية")],
      ["عباس حميد ياسين", of("الحاسوب", "التربية الأخلاقية", "التربية الفنية", "التربية الرياضية")],
      ["إيمان صادق حسون", of("الحاسوب", "التربية الأخلاقية", "التربية الفنية", "التربية الرياضية")],
    ];
    const created = [];
    for (const [name, subjectIds] of specialists) created.push(await teacher(page, server.baseUrl, name, subjectIds));
    // One existing assignment the suggester must keep: Arabic of section أ in the first grade, by Zainab.
    const matrix = await api<{ stage: { lines: Array<{ entryId: number; subjectName: string }>; sections: Array<{ sectionId: number; label: string }> } }>(
      page, server.baseUrl, "GET", `/academic-years/${yearId}/workload/matrix`);
    const arabicLine = matrix.stage.lines.find((line) => line.subjectName === "اللغة العربية")!.entryId;
    const sectionA = matrix.stage.sections.find((section) => section.label === "أ")!.sectionId;
    await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/workload/cell`, { sectionId: sectionA, entryId: arabicLine, teacherId: created[1].id, assignmentId: null, version: null });

    await goToSection(page, school.nav.workload);
    const ux = new UxMeter("phase3 intermediate 12 sections");
    const suggester = page.locator(".page-card", { hasText: workload.suggester.title });
    await ux.act(suggester.getByRole("button", { name: workload.suggester.preview }));
    await expect(suggester.locator(".plan-lines").first()).toBeVisible();
    const first = await suggester.locator(".plan-lines").allInnerTexts();
    await ux.act(suggester.getByRole("button", { name: workload.suggester.preview }));
    await expect(suggester.getByRole("button", { name: workload.suggester.preview })).toBeEnabled();
    await expect.poll(() => suggester.locator(".plan-lines").allInnerTexts()).toEqual(first); // deterministic: the same list, line for line
    await expect(suggester.getByText(workload.suggester.unassigned)).toHaveCount(0);
    await expectNoSeriousA11yViolations(page, "suggester preview");
    await expectBreakpointScreenshots(page, "phase3-suggester-preview");
    await ux.act(suggester.getByRole("button", { name: workload.suggester.apply }));
    await ux.act(page.getByRole("dialog", { name: workload.suggester.confirmTitle }).getByRole("button", { name: workload.suggester.apply }));
    await expect(suggester.getByRole("status")).toBeVisible();
    console.log(ux.report());
    expect(ux.typed).toBe(0);

    // Applied = previewed: every cell now has a teacher and Zainab still teaches Arabic in أ.
    const after = await api<{ stage: { sections: Array<{ sectionId: number; cells: Array<{ entryId: number; teacherId: number | null }> }> } }>(
      page, server.baseUrl, "GET", `/academic-years/${yearId}/workload/matrix`);
    expect(after.stage.sections.find((section) => section.sectionId === sectionA)!.cells.find((cell) => cell.entryId === arabicLine)!.teacherId).toBe(created[1].id);
    await page.reload();
    await expect(page.locator(".workload-flag", { hasText: workload.unassigned })).toHaveCount(0);
    const report = await api<{ ready: boolean; lines: number; assigned: number }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/readiness/`);
    expect([report.ready, report.assigned]).toEqual([true, report.lines]);
  } finally {
    await server.stop();
  }
});

test("(c) resource shortage and (d) protection of subject, section and stage", async ({ browser, page }) => {
  const server = new ApiServer();
  await server.start(browser, "phase3-resource");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Resource-Owner-1");
    const { yearId } = await prepareSchool(page, server.baseUrl, { schoolType: "intermediate", shiftMode: "morning", grades: [{ gradeKey: "intermediate-1", sections: 3 }] });
    const field = await api<{ id: number }>(page, server.baseUrl, "POST", "/resources/", { name: "الساحة الرياضية", kind: "field", capacity: 1, notes: null, version: 0 });
    // Physical education only in lessons 2–3 of Sunday: 2 slots, the field takes one section at a time; 3 sections need it.
    const blocked = [7, 1, 2, 3, 4].flatMap((day) => [1, 2, 3, 4, 5, 6, 7].filter((lesson) => day !== 7 || (lesson !== 2 && lesson !== 3)).map((lesson) => ({ day, lessonNumber: lesson })));
    const pe = await api<Subject>(page, server.baseUrl, "POST", "/subjects/", {
      name: "التربية الرياضية", colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false,
      requiresDoublePeriod: false, blockedPeriods: blocked, notes: null, requiredResourceId: field.id, version: 0,
    });
    const table = await api<{ stages: Array<{ id: number }> }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/curriculum`);
    await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/curriculum/cell`, { stageId: table.stages[0].id, subjectId: pe.id, label: null, weeklyLessons: 1, entryId: null, version: null });
    const coach = await teacher(page, server.baseUrl, "عباس حميد ياسين", [pe.id]);
    const matrix = await api<{ stage: { sections: Array<{ sectionId: number }> } }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/workload/matrix`);
    for (const section of matrix.stage.sections) {
      await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/workload/bulk/class-teacher`, { teacherId: coach.id, sectionId: section.sectionId, entryIds: [], overwrite: false });
    }

    await page.goto(`${server.baseUrl}/readiness`);
    const message = readiness.messages.RESOURCE_OVER_CAPACITY(isolate("الساحة الرياضية"), isolate("الدوام الصباحي"), "٣ حصص", "حصتان", "حصة واحدة");
    await expect(page.getByText(message)).toBeVisible();
    await expectLayout(page, "resource shortage readiness");
    await expectBreakpointScreenshots(page, "phase3-resource-shortage", [page.locator(".readiness-meta")]); // run time and hash change

    // The fix: capacity 2 on the resources tab, then the shortage is gone.
    await goToSection(page, school.nav.resources);
    await page.locator(".ui-expandable-toggle", { hasText: "الساحة الرياضية" }).click();
    const editor = page.locator("form", { has: page.getByRole("button", { name: school.resources.save }) });
    await editor.getByRole("button", { name: school.resources.increaseCapacity }).click();
    await editor.getByRole("button", { name: school.resources.save }).click();
    await expect(page.getByRole("status").filter({ hasText: school.resources.saved })).toBeVisible();
    await page.goto(`${server.baseUrl}/readiness`);
    await expect(page.getByText(message)).toHaveCount(0);

    // (d) The subject, a section and the stage are referenced: delete dialogs list them and keep «حذف» disabled.
    const blockedDelete = async (name: string, title: string, dependent: RegExp) => {
      await page.getByRole("button", { name: `${school.common.delete} ${isolate(name)}`, exact: true }).click();
      const dialog = page.getByRole("dialog", { name: title });
      await expect(dialog.getByText(dependent).first()).toBeVisible();
      await expect(dialog.getByRole("button", { name: school.common.delete })).toBeDisabled();
      await dialog.getByRole("button", { name: messages.app.cancel }).click();
    };
    await goToSection(page, school.nav.subjects);
    await blockedDelete("التربية الرياضية", school.subjects.deleteTitle, /بند واحد في المنهج/);
    await goToSection(page, school.nav.stagesSections);
    await blockedDelete("الأول المتوسط", school.stagesSections.deleteStageTitle, /٣ شعب/);
    // The only stage is already selected, so its sections are listed below.
    await blockedDelete("أ", school.stagesSections.deleteSectionTitle, /نصاب واحد/);
  } finally {
    await server.stop();
  }
});

test("(f) orphan blocked periods after lessons per day shrink: previewed, then removed on confirmation", async ({ browser, page }) => {
  const server = new ApiServer();
  await server.start(browser, "phase3-orphans");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Orphans-Owner-1");
    const { yearId, shifts } = await prepareSchool(page, server.baseUrl, { schoolType: "intermediate", shiftMode: "morning", grades: [{ gradeKey: "intermediate-1", sections: 1 }] });
    await api(page, server.baseUrl, "POST", "/teachers/", {
      fullName: "حيدر عبد الأمير صالح", shortName: null, offDays: [], blockedPeriods: [{ day: 7, lessonNumber: 7 }, { day: 1, lessonNumber: 2 }],
      fullyReleased: false, releaseReason: null, releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null, notes: null, version: 0,
    });
    const shift = shifts[0];
    const current = await api<Paged<{ id: number; version: number }>>(page, server.baseUrl, "GET", `/academic-years/${yearId}/shifts/?pageSize=10`);
    const version = current.items.find((item) => item.id === shift.id)!.version;
    await api(page, server.baseUrl, "PUT", `/academic-years/${yearId}/shifts/${shift.id}/day-lessons`, {
      dayLessons: [7, 1, 2, 3, 4].map((day) => ({ day, lessons: 6 })), version, confirmStageChanges: true,
    });

    await goToSection(page, school.nav.scheduleStructure);
    const orphan = school.orphanBlocked;
    await expect(page.getByText(orphan.notice(arab(1)))).toBeVisible();
    await page.getByRole("button", { name: orphan.review }).click();
    const dialog = page.getByRole("dialog", { name: orphan.title });
    await expect(dialog.getByText(orphan.ownerSlots(orphan.teacher("حيدر عبد الأمير صالح"), orphan.slot("الأحد", arab(7))))).toBeVisible();
    await expectNoSeriousA11yViolations(page, "orphan review dialog");
    await dialog.getByRole("button", { name: orphan.remove }).click();
    await expect(page.getByRole("status").filter({ hasText: orphan.removed })).toBeVisible();
    await expect(page.getByText(orphan.notice(arab(1)))).toHaveCount(0);
    // The slot inside the grid (Monday, lesson 2) stays.
    const list = await api<Paged<{ blockedPeriods: Array<{ day: number; lessonNumber: number }> }>>(page, server.baseUrl, "GET", "/teachers/?pageSize=10");
    expect(list.items[0].blockedPeriods).toEqual([{ day: 1, lessonNumber: 2 }]);
    await expectLayout(page, "timing after orphan cleanup");
  } finally {
    await server.stop();
  }
});
