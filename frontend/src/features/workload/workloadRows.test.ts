import { describe, expect, it } from "vitest";
import type { TeacherLoad, WorkloadRow } from "./workloadApi";
import { filterRows, rowStatus, totals, unassignedFilter } from "./workloadRows";

const row = (overrides: Partial<WorkloadRow>): WorkloadRow => ({
  sectionId: 1, stageId: 1, stageName: "الأول", sectionLabel: "أ", entryId: 1, subjectId: 1, subjectName: "الرياضيات", label: null,
  weeklyLessons: 5, assignmentId: null, teacherId: null, version: null, outsideSpecialization: false, ...overrides,
});
const load = (teacherId: number, status: TeacherLoad["status"]): TeacherLoad => ({
  teacherId, fullName: "م", shortName: "م", specializationIds: [], assignedLessons: 0, maxPerWeek: null, available: 30, limit: 24, status,
  released: false, assignments: [], version: 1,
});

describe("workload rows (MF2)", () => {
  const rows = [row({}), row({ entryId: 2, teacherId: 7, assignmentId: 3, version: 1, weeklyLessons: 4 }), row({ stageId: 2, sectionId: 2, teacherId: 8, weeklyLessons: 6 })];
  const loads = [load(7, "within"), load(8, "over")];

  it("marks each row assigned, missing or overloaded (never by colour alone in the UI)", () => {
    expect(rows.map((item) => rowStatus(item, loads))).toEqual(["missing", "assigned", "overloaded"]);
  });

  it("filters by stage and by teacher, including «بدون معلم»", () => {
    expect(filterRows(rows, { stageId: 2, teacherId: null })).toHaveLength(1);
    expect(filterRows(rows, { stageId: null, teacherId: 7 }).map((item) => item.entryId)).toEqual([2]);
    expect(filterRows(rows, { stageId: null, teacherId: unassignedFilter }).map((item) => item.teacherId)).toEqual([null]);
  });

  it("counts rows and weekly lessons with a teacher, never «٠ من ٠» when there are rows", () => {
    expect(totals(rows)).toEqual({ assignedRows: 2, rows: 3, assignedLessons: 10, lessons: 15 });
    expect(totals([])).toEqual({ assignedRows: 0, rows: 0, assignedLessons: 0, lessons: 0 });
  });
});
