import type { TeacherLoad, WorkloadRow } from "./workloadApi";

/** A row's state: a teacher is assigned, none is, or the assigned teacher is above their weekly limit. */
export type RowStatus = "assigned" | "missing" | "overloaded";

export function rowStatus(row: WorkloadRow, loads: readonly TeacherLoad[]): RowStatus {
  if (row.teacherId === null) return "missing";
  return loads.find((load) => load.teacherId === row.teacherId)?.status === "over" ? "overloaded" : "assigned";
}

/** «بدون معلم» in the teacher filter. */
export const unassignedFilter = -1;

export type RowFilter = { stageId: number | null; teacherId: number | null };

export function filterRows(rows: readonly WorkloadRow[], filter: RowFilter): WorkloadRow[] {
  return rows.filter((row) => (filter.stageId === null || row.stageId === filter.stageId)
    && (filter.teacherId === null || (filter.teacherId === unassignedFilter ? row.teacherId === null : row.teacherId === filter.teacherId)));
}

/** Rows and weekly lessons that have a teacher, out of all of them (the step's summary line). */
export function totals(rows: readonly WorkloadRow[]) {
  const assigned = rows.filter((row) => row.teacherId !== null);
  return {
    assignedRows: assigned.length,
    rows: rows.length,
    assignedLessons: assigned.reduce((sum, row) => sum + row.weeklyLessons, 0),
    lessons: rows.reduce((sum, row) => sum + row.weeklyLessons, 0),
  };
}
