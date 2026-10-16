import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { refreshQueries } from "../../lib/refreshQueries";
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
  /** The one resource the subject needs (Phase 3 §2.2), or null. */
  requiredResourceId: number | null;
  isArchived: boolean;
  archivedAt: string | null;
  version: number;
  /** The owner's daily limit of the subject in one section (1 to 6), or null for the automatic limit. */
  dailyCap?: number | null;
  /** The subject's daily limit for one grade or one section; the most specific wins (section, grade, subject). */
  capOverrides?: CapOverride[];
};
export type CapScope = "stage" | "section";
export type CapOverride = { scope: CapScope; scopeId: number; cap: number };
export type SubjectInput = Omit<Subject, "id" | "isArchived" | "archivedAt" | "dailyCap" | "capOverrides">;

/** The daily limit choices (1 to 6 lessons a day); «تلقائي» is the absence of a value. */
export const dailyCapChoices = [1, 2, 3, 4, 5, 6] as const;

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
      // The curriculum table and the subject suggestions list subjects too (fix B6: no reload needed).
      await Promise.all([refreshQueries(queryClient, [subjectsKey, ["curriculum"], ["suggested-subjects"], ["workload"]]), refreshSchoolData()]);
    },
  });
}

export function useSaveSubject() {
  return useSubjectsMutation(({ id, input }: { id: number | null; input: SubjectInput }) =>
    id === null
      ? apiRequest<Subject>(`${subjectsPath}/`, "POST", input)
      : apiRequest<Subject>(`${subjectsPath}/${id}`, "PUT", input));
}

/** Sets (or clears, with null) the daily limit of one subject: «أولويات الجدول › متقدم». */
export function useSetDailyCap() {
  return useSubjectsMutation(({ subject, dailyCap }: { subject: Subject; dailyCap: number | null }) =>
    apiRequest<Subject>(`${subjectsPath}/${subject.id}/daily-cap`, "PUT", { dailyCap, version: subject.version }));
}

/** Sets (or clears, with null) the daily limit of one subject for one grade or section: «أولويات الجدول › متقدم». */
export function useSetCapOverride() {
  return useSubjectsMutation(({ subject, scope, scopeId, cap }: { subject: Subject; scope: CapScope; scopeId: number; cap: number | null }) =>
    apiRequest<Subject>(`${subjectsPath}/${subject.id}/cap-override`, "PUT", { scope, scopeId, cap, version: subject.version }));
}

/** Archive or restore one subject and get it back (its new version is needed for an undo). */
export function useArchiveSubject() {
  return useSubjectsMutation(({ subject, archived }: { subject: Subject; archived: boolean }) =>
    apiRequest<Subject>(`${subjectsPath}/${subject.id}/${archived ? "archive" : "restore"}`, "POST", { version: subject.version }));
}

export function useSubjectAction() {
  return useSubjectsMutation(({ subject, action }: { subject: Subject; action: "archive" | "restore" | "delete" }) =>
    action === "delete"
      ? apiRequest<unknown>(`${subjectsPath}/${subject.id}?version=${subject.version}`, "DELETE")
      : apiRequest<unknown>(`${subjectsPath}/${subject.id}/${action}`, "POST", { version: subject.version }));
}
