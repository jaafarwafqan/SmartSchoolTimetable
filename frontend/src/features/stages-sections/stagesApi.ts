import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";

export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };
export type Stage = { id: number; academicYearId: number; name: string; displayOrder: number; isArchived: boolean; archivedAt: string | null; version: number };
export type Section = { id: number; stageId: number; label: string; shiftId: number; studentCount: number | null; weeklyCapacity: number; isArchived: boolean; archivedAt: string | null; version: number };
const stageKey = (yearId: number | undefined) => ["stages", yearId] as const;
const sectionKey = (yearId: number | undefined, stageId: number | undefined) => ["sections", yearId, stageId] as const;
export function useStages(yearId: number | undefined, includeArchived: boolean) {
  return useQuery({ queryKey: [...stageKey(yearId), includeArchived], enabled: yearId !== undefined, queryFn: () => apiRequest<Page<Stage>>(`/api/v1/academic-years/${yearId}/stages/?pageSize=100&includeArchived=${includeArchived}`) });
}
export function useSections(yearId: number | undefined, stageId: number | undefined, includeArchived: boolean) {
  return useQuery({ queryKey: [...sectionKey(yearId, stageId), includeArchived], enabled: yearId !== undefined && stageId !== undefined, queryFn: () => apiRequest<Page<Section>>(`/api/v1/academic-years/${yearId}/stages/${stageId}/sections?pageSize=100&includeArchived=${includeArchived}`) });
}
function useSave<T>(path: (value: T) => string, method: "PUT" | "POST", keys: readonly (readonly unknown[])[]) {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ body, url }: { body: T; url?: string }) => apiRequest(url ?? path(body), method, body), onSuccess: async () => { await Promise.all(keys.map(queryKey => client.invalidateQueries({ queryKey }))); } });
}
export function useStageSave(yearId: number) { return useSave<{ name: string; displayOrder: number; version: number }>(() => `/api/v1/academic-years/${yearId}/stages/`, "POST", [stageKey(yearId)]); }
export function useStageEdit(yearId: number, id: number) { return useSave<{ name: string; displayOrder: number; version: number }>(() => `/api/v1/academic-years/${yearId}/stages/${id}`, "PUT", [stageKey(yearId)]); }
export function useSectionSave(yearId: number, stageId: number) { return useSave<{ label: string; shiftId: number; studentCount: number | null; version: number }>(() => `/api/v1/academic-years/${yearId}/stages/${stageId}/sections`, "POST", [sectionKey(yearId, stageId)]); }
export function useSectionEdit(yearId: number, stageId: number, id: number) { return useSave<{ label: string; shiftId: number; studentCount: number | null; version: number }>(() => `/api/v1/academic-years/${yearId}/stages/${stageId}/sections/${id}`, "PUT", [sectionKey(yearId, stageId)]); }
export function useArchive(url: string, keys: readonly (readonly unknown[])[]) { return useSave<{ version: number }>(() => url, "POST", keys); }
