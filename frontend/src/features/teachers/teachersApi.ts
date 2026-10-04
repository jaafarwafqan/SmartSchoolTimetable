import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { useRefreshSchoolData } from "../../lib/schoolContext";
import type { Paged } from "../academic-years/yearsApi";
import type { BlockedSlot } from "../timetable-structure/scheduleApi";

export type Teacher = {
  id: number;
  fullName: string;
  shortName: string;
  offDays: number[];
  blockedPeriods: BlockedSlot[];
  fullyReleased: boolean;
  releaseReason: string | null;
  releaseFrom: string | null;
  releaseTo: string | null;
  maxLessonsPerDay: number | null;
  maxLessonsPerWeek: number | null;
  notes: string | null;
  isArchived: boolean;
  archivedAt: string | null;
  version: number;
};
export type TeacherInput = Omit<Teacher, "id" | "isArchived" | "archivedAt">;
export type BulkStatus = "ready" | "tooLong" | "duplicateInList" | "exists" | "noShortName";
export type BulkPreview = { lines: { line: number; fullName: string; shortName: string | null; status: BulkStatus }[]; readyCount: number };

const teachersKey = ["teachers"] as const;
const teachersPath = "/api/v1/teachers";

export type TeacherListParams = { search: string; page: number; pageSize: number; includeArchived: boolean; releasedOnly: boolean };

export function useTeachers(params: TeacherListParams) {
  const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize), includeArchived: String(params.includeArchived) });
  if (params.search.trim()) query.set("search", params.search.trim());
  if (params.releasedOnly) query.set("released", "true");
  return useQuery({
    queryKey: [...teachersKey, params],
    queryFn: () => apiRequest<Paged<Teacher>>(`${teachersPath}/?${query.toString()}`),
    placeholderData: keepPreviousData,
  });
}

function useTeachersMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([queryClient.invalidateQueries({ queryKey: teachersKey }), refreshSchoolData()]);
    },
  });
}

export function useSaveTeacher() {
  return useTeachersMutation(({ id, input }: { id: number | null; input: TeacherInput }) =>
    id === null
      ? apiRequest<Teacher>(`${teachersPath}/`, "POST", input)
      : apiRequest<Teacher>(`${teachersPath}/${id}`, "PUT", input));
}

export function useTeacherAction() {
  return useTeachersMutation(({ teacher, action }: { teacher: Teacher; action: "archive" | "restore" | "delete" }) =>
    action === "delete"
      ? apiRequest<unknown>(`${teachersPath}/${teacher.id}?version=${teacher.version}`, "DELETE")
      : apiRequest<unknown>(`${teachersPath}/${teacher.id}/${action}`, "POST", { version: teacher.version }));
}

export function usePreviewBulk() {
  return useMutation({ mutationFn: (names: string[]) => apiRequest<BulkPreview>(`${teachersPath}/bulk/preview`, "POST", { names }) });
}

export function useCreateBulk() {
  return useTeachersMutation((names: string[]) => apiRequest<{ created: number }>(`${teachersPath}/bulk`, "POST", { names }));
}
