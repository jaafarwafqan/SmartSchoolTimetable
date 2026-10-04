import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { useRefreshSchoolData } from "../../lib/schoolContext";
import type { Paged } from "../academic-years/yearsApi";
import type { BlockedSlot } from "../timetable-structure/scheduleApi";

export type Subject = {
  id: number;
  name: string;
  colorIndex: number;
  priority: number;
  distributionEnabled: boolean;
  spreadAcrossDays: boolean;
  heavy: boolean;
  requiresDoublePeriod: boolean;
  blockedPeriods: BlockedSlot[];
  notes: string | null;
  isArchived: boolean;
  archivedAt: string | null;
  version: number;
};
export type SubjectInput = Omit<Subject, "id" | "isArchived" | "archivedAt">;

const subjectsKey = ["subjects"] as const;
const subjectsPath = "/api/v1/subjects";

export function useSubjects(params: { search: string; page: number; pageSize: number; includeArchived: boolean }) {
  const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize), includeArchived: String(params.includeArchived) });
  if (params.search.trim()) query.set("search", params.search.trim());
  return useQuery({
    queryKey: [...subjectsKey, params],
    queryFn: () => apiRequest<Paged<Subject>>(`${subjectsPath}/?${query.toString()}`),
    placeholderData: keepPreviousData,
  });
}

function useSubjectsMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([queryClient.invalidateQueries({ queryKey: subjectsKey }), refreshSchoolData()]);
    },
  });
}

export function useSaveSubject() {
  return useSubjectsMutation(({ id, input }: { id: number | null; input: SubjectInput }) =>
    id === null
      ? apiRequest<Subject>(`${subjectsPath}/`, "POST", input)
      : apiRequest<Subject>(`${subjectsPath}/${id}`, "PUT", input));
}

export function useSubjectAction() {
  return useSubjectsMutation(({ subject, action }: { subject: Subject; action: "archive" | "restore" | "delete" }) =>
    action === "delete"
      ? apiRequest<unknown>(`${subjectsPath}/${subject.id}?version=${subject.version}`, "DELETE")
      : apiRequest<unknown>(`${subjectsPath}/${subject.id}/${action}`, "POST", { version: subject.version }));
}
