import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { useRefreshSchoolData } from "../../lib/schoolContext";

export type Term = { id: number; name: string; startDate: string; endDate: string; isCurrent: boolean };

export type AcademicYear = {
  id: number;
  label: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  terms: Term[];
  version: number;
  /** #87: how many Iraqi official holidays were added to the calendar when the year was created (create only). */
  holidaysAdded?: number;
};

export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number };
export type ListParams = { search: string; page: number; pageSize: number };

export const yearsKey = ["academic-years"] as const;
const yearsPath = "/api/v1/academic-years";

export function listQueryString(params: ListParams & { includeArchived?: boolean; sort?: string }): string {
  const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize) });
  if (params.search.trim()) query.set("search", params.search.trim());
  if (params.includeArchived) query.set("includeArchived", "true");
  if (params.sort) query.set("sort", params.sort);
  return query.toString();
}

export function useAcademicYears(params: ListParams) {
  return useQuery({
    queryKey: [...yearsKey, params],
    queryFn: () => apiRequest<Paged<AcademicYear>>(`${yearsPath}?${listQueryString(params)}`),
    placeholderData: keepPreviousData,
  });
}

function useYearMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([queryClient.invalidateQueries({ queryKey: yearsKey }), refreshSchoolData()]);
    },
  });
}

export type YearInput = { label: string; startDate: string; endDate: string; version: number; copyStructureFromYearId?: number | null };
export type TermInput = { name: string; startDate: string; endDate: string; version: number };

export function useSaveYear() {
  return useYearMutation(({ id, input }: { id: number | null; input: YearInput }) =>
    id === null
      ? apiRequest<AcademicYear>(yearsPath, "POST", input)
      : apiRequest<AcademicYear>(`${yearsPath}/${id}`, "PUT", input));
}

export function useDeleteYear() {
  return useYearMutation(({ id, version }: { id: number; version: number }) =>
    apiRequest<void>(`${yearsPath}/${id}?version=${version}`, "DELETE"));
}

export function useMakeYearCurrent() {
  return useYearMutation(({ id, version }: { id: number; version: number }) =>
    apiRequest<AcademicYear>(`${yearsPath}/${id}/make-current`, "POST", { version }));
}

export function useSaveTerm() {
  return useYearMutation(({ yearId, termId, input }: { yearId: number; termId: number | null; input: TermInput }) =>
    termId === null
      ? apiRequest<AcademicYear>(`${yearsPath}/${yearId}/terms`, "POST", input)
      : apiRequest<AcademicYear>(`${yearsPath}/${yearId}/terms/${termId}`, "PUT", input));
}

export function useDeleteTerm() {
  return useYearMutation(({ yearId, termId, version }: { yearId: number; termId: number; version: number }) =>
    apiRequest<AcademicYear>(`${yearsPath}/${yearId}/terms/${termId}?version=${version}`, "DELETE"));
}

export function useMakeTermCurrent() {
  return useYearMutation(({ yearId, termId, version }: { yearId: number; termId: number; version: number }) =>
    apiRequest<AcademicYear>(`${yearsPath}/${yearId}/terms/${termId}/make-current`, "POST", { version }));
}
