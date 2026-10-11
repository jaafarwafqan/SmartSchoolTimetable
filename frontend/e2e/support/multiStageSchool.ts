import type { Page } from "@playwright/test";
import { api, prepareSchool } from "./api";

type Subject = { id: number };
type Curriculum = { stages: Array<{ id: number }> };

/**
 * A fictional intermediate school entered through the normal endpoints (test-only data): three grades of two sections each (three
 * "stages" in the printed whole-school grid), four subjects of 28 lessons a week, twelve teachers (three per subject) and the
 * assignment suggester. Used to look at stage rows, the readable-size split and colour or black-and-white printing.
 */
export async function seedMultiStageSchool(page: Page, baseUrl: string) {
  const { yearId } = await prepareSchool(page, baseUrl, {
    schoolType: "intermediate", shiftMode: "morning",
    grades: [{ gradeKey: "intermediate-1", sections: 2 }, { gradeKey: "intermediate-2", sections: 2 }, { gradeKey: "intermediate-3", sections: 2 }],
  });
  const plan: Array<[string, number, number]> = [["الرياضيات", 8, 1], ["اللغة العربية", 8, 2], ["العلوم", 6, 3], ["التربية الفنية", 6, 4]];
  const subjects: Array<{ id: number; lessons: number }> = [];
  for (const [name, lessons, colorIndex] of plan) {
    const subject = await api<Subject>(page, baseUrl, "POST", "/subjects/", {
      name, colorIndex, priority: 0, distributionEnabled: true, spreadAcrossDays: true, heavy: name === "الرياضيات",
      requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
    });
    subjects.push({ id: subject.id, lessons });
  }
  const curriculum = await api<Curriculum>(page, baseUrl, "GET", `/academic-years/${yearId}/curriculum`);
  for (const stage of curriculum.stages) {
    for (const subject of subjects) {
      await api(page, baseUrl, "PUT", `/academic-years/${yearId}/curriculum/cell`, {
        stageId: stage.id, subjectId: subject.id, label: null, weeklyLessons: subject.lessons, entryId: null, version: null,
      });
    }
  }
  const names = ["أحمد علي حسن", "سعد كاظم حسن", "زينب جاسم محمد", "مريم عادل كريم", "حسين رعد سالم", "ليلى ماجد فاضل",
    "علي حيدر نوري", "هدى سلام صالح", "كرار وليد عباس", "رنا طارق جميل", "يوسف ثامر عدنان", "سارة باسم لطيف"];
  for (const [index, fullName] of names.entries()) {
    await api(page, baseUrl, "POST", "/teachers/", {
      fullName, shortName: `${fullName.split(" ")[0]} ${fullName.split(" ")[1]}`, offDays: [], blockedPeriods: [], fullyReleased: false,
      releaseReason: null, releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null,
      notes: null, version: 0, specializationIds: [subjects[index % subjects.length].id],
    });
  }
  await api(page, baseUrl, "POST", `/academic-years/${yearId}/workload/suggestions/apply`, { confirm: true });
  return yearId;
}
