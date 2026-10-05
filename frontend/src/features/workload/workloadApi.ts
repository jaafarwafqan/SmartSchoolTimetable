import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { refreshQueries } from "../../lib/refreshQueries";

export type WorkloadLine = { entryId: number; subjectId: number; subjectName: string; colorIndex: number; label: string | null; weeklyLessons: number };
export type WorkloadCell = { entryId: number; assignmentId: number | null; teacherId: number | null; version: number | null; outsideSpecialization: boolean };
export type WorkloadSectionRow = { sectionId: number; label: string; shiftName: string; assignedLines: number; totalLines: number; assignedLessons: number; totalLessons: number; cells: WorkloadCell[] };
export type WorkloadStage = { stageId: number; stageName: string; lines: WorkloadLine[]; sections: WorkloadSectionRow[] };
export type WorkloadStageSummary = { stageId: number; stageName: string; assignedCells: number; totalCells: number };
export type WorkloadMatrix = { stages: WorkloadStageSummary[]; stage: WorkloadStage | null };

export type LoadStatus = "within" | "near" | "over";
export type TeacherAssignment = { assignmentId: number; sectionId: number; stageName: string; sectionLabel: string; entryId: number; subjectName: string; label: string | null; weeklyLessons: number; outsideSpecialization: boolean };
export type TeacherLoad = {
  teacherId: number;
  fullName: string;
  shortName: string;
  specializationIds: number[];
  assignedLessons: number;
  maxPerWeek: number | null;
  available: number;
  limit: number;
  status: LoadStatus;
  released: boolean;
  assignments: TeacherAssignment[];
  version: number;
};

export type CellInput = { sectionId: number; entryId: number; teacherId: number | null; assignmentId: number | null; version: number | null };
export type PlanAction = "create" | "replace" | "skip" | "unchanged" | "transfer" | "remove";
export type WorkloadPlanLine = { sectionId: number; stageName: string; sectionLabel: string; entryId: number; subjectName: string; label: string | null; weeklyLessons: number; currentTeacher: string | null; newTeacher: string | null; action: PlanAction };
export type TeacherLoadChange = { teacherId: number; fullName: string; before: number; after: number; limit: number };
export type WorkloadPlan = { lines: WorkloadPlanLine[]; changes: number; loads: TeacherLoadChange[] };
export type SuggestedAssignment = { sectionId: number; stageName: string; sectionLabel: string; entryId: number; subjectName: string; label: string | null; weeklyLessons: number; teacherId: number; teacherName: string };
export type UnassignedSuggestion = { sectionId: number; stageName: string; sectionLabel: string; entryId: number; subjectName: string; label: string | null; weeklyLessons: number; reason: string };
export type SuggestionTeacherLoad = { teacherId: number; teacherName: string; before: number; after: number; limit: number };
export type AssignmentSuggestionPlan = { assignments: SuggestedAssignment[]; unassigned: UnassignedSuggestion[]; loads: SuggestionTeacherLoad[] };

export type BulkKind = "across-stage" | "class-teacher" | "transfer" | "remove";
export type BulkInput =
  | { kind: "across-stage"; body: { teacherId: number; entryId: number; overwrite: boolean } }
  | { kind: "class-teacher"; body: { teacherId: number; sectionId: number; entryIds: number[]; overwrite: boolean } }
  | { kind: "transfer"; body: { fromTeacherId: number; toTeacherId: number } }
  | { kind: "remove"; body: { teacherId: number } };

const workloadKey = ["workload"] as const;
const workloadPath = (yearId: number) => `/api/v1/academic-years/${yearId}/workload`;

export function useWorkloadMatrix(yearId: number | null, stageId: number | null) {
  return useQuery({
    queryKey: [...workloadKey, "matrix", yearId, stageId],
    queryFn: () => apiRequest<WorkloadMatrix>(`${workloadPath(yearId ?? 0)}/matrix${stageId === null ? "" : `?stageId=${stageId}`}`),
    enabled: yearId !== null,
    placeholderData: keepPreviousData,
  });
}

export function useTeacherLoads(yearId: number | null) {
  return useQuery({
    queryKey: [...workloadKey, "teachers", yearId],
    queryFn: () => apiRequest<TeacherLoad[]>(`${workloadPath(yearId ?? 0)}/teachers`),
    enabled: yearId !== null,
  });
}

/** Any workload change refreshes the matrix, the loads, the teachers list (load badges) and the references. */
function useWorkloadMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => { await refreshQueries(queryClient, [workloadKey, ["teachers"], ["references"], ["dashboard"]]); },
  });
}

export function useSetWorkloadCell(yearId: number) {
  return useWorkloadMutation((input: CellInput) => apiRequest<WorkloadStage>(`${workloadPath(yearId)}/cell`, "PUT", input));
}

export function useAddSpecialization() {
  return useWorkloadMutation(({ teacherId, subjectId, version }: { teacherId: number; subjectId: number; version: number }) =>
    apiRequest<unknown>(`/api/v1/teachers/${teacherId}/specializations/${subjectId}`, "POST", { version }));
}

/** Preview (saves nothing, no refresh) and apply of one bulk action. */
export function useBulkPlan(yearId: number) {
  const preview = useMutation({ mutationFn: (input: BulkInput) => apiRequest<WorkloadPlan>(`${workloadPath(yearId)}/bulk/${input.kind}/preview`, "POST", input.body) });
  const apply = useWorkloadMutation((input: BulkInput) => apiRequest<WorkloadPlan>(`${workloadPath(yearId)}/bulk/${input.kind}`, "POST", input.body));
  return { preview, apply };
}

export function useAssignmentSuggestions(yearId: number) {
  const preview = useMutation({
    mutationFn: () => apiRequest<AssignmentSuggestionPlan>(`${workloadPath(yearId)}/suggestions/preview`),
  });
  const apply = useWorkloadMutation(() => apiRequest<AssignmentSuggestionPlan>(`${workloadPath(yearId)}/suggestions/apply`, "POST", { confirm: true }));
  return { preview, apply };
}

/** Teachers offered for a line: its subject's specialists (and the current teacher), or everyone with «عرض الجميع». */
export function teacherChoices(loads: readonly TeacherLoad[], subjectId: number, currentTeacherId: number | null, showAll: boolean): TeacherLoad[] {
  return loads.filter((load) => showAll || load.teacherId === currentTeacherId || load.specializationIds.includes(subjectId));
}

/** Teachers above their limit, most over first (quick shortage warnings). */
export function overloaded(loads: readonly TeacherLoad[]): TeacherLoad[] {
  return loads.filter((load) => load.status === "over").sort((a, b) => (b.assignedLessons - b.limit) - (a.assignedLessons - a.limit));
}
