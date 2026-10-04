import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { useRefreshSchoolData } from "../../lib/schoolContext";
import type { Paged } from "../academic-years/yearsApi";

export type Stage = { id: number; academicYearId: number; name: string; displayOrder: number; isArchived: boolean; archivedAt: string | null; version: number };
export type Section = {
  id: number;
  stageId: number;
  label: string;
  shiftId: number;
  studentCount: number | null;
  weeklyCapacity: number;
  isArchived: boolean;
  archivedAt: string | null;
  version: number;
};
export type StageInput = { name: string; displayOrder: number; version: number };
export type SectionInput = { label: string; shiftId: number; studentCount: number | null; version: number };

const stagesKey = ["stages"] as const;
const sectionsKey = ["sections"] as const;
const stagesPath = (yearId: number) => `/api/v1/academic-years/${yearId}/stages`;
const sectionsPath = (yearId: number, stageId: number) => `${stagesPath(yearId)}/${stageId}/sections`;

export function useStages(yearId: number, search: string, includeArchived: boolean) {
  const query = new URLSearchParams({ pageSize: "100", includeArchived: String(includeArchived) });
  if (search.trim()) query.set("search", search.trim());
  return useQuery({
    queryKey: [...stagesKey, yearId, search.trim(), includeArchived],
    queryFn: () => apiRequest<Paged<Stage>>(`${stagesPath(yearId)}/?${query.toString()}`),
    placeholderData: keepPreviousData,
  });
}

export function useSections(yearId: number, stageId: number, includeArchived: boolean) {
  return useQuery({
    queryKey: [...sectionsKey, yearId, stageId, includeArchived],
    queryFn: () => apiRequest<Paged<Section>>(`${sectionsPath(yearId, stageId)}?pageSize=100&includeArchived=${includeArchived}`),
  });
}

/** Stage and section changes refresh both lists (archive rules span them) and the dashboard. */
function useStagesMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: stagesKey }),
        queryClient.invalidateQueries({ queryKey: sectionsKey }),
        refreshSchoolData(),
      ]);
    },
  });
}

export function useSaveStage(yearId: number) {
  return useStagesMutation(({ id, input }: { id: number | null; input: StageInput }) =>
    id === null
      ? apiRequest<Stage>(`${stagesPath(yearId)}/`, "POST", input)
      : apiRequest<Stage>(`${stagesPath(yearId)}/${id}`, "PUT", input));
}

export function useStageAction(yearId: number) {
  return useStagesMutation(({ stage, action }: { stage: Stage; action: "archive" | "restore" | "delete" }) =>
    action === "delete"
      ? apiRequest<unknown>(`${stagesPath(yearId)}/${stage.id}?version=${stage.version}`, "DELETE")
      : apiRequest<unknown>(`${stagesPath(yearId)}/${stage.id}/${action}`, "POST", { version: stage.version }));
}

export function useSaveSection(yearId: number, stageId: number) {
  return useStagesMutation(({ id, input }: { id: number | null; input: SectionInput }) =>
    id === null
      ? apiRequest<Section>(sectionsPath(yearId, stageId), "POST", input)
      : apiRequest<Section>(`${sectionsPath(yearId, stageId)}/${id}`, "PUT", input));
}

export function useSectionAction(yearId: number, stageId: number) {
  return useStagesMutation(({ section, action }: { section: Section; action: "archive" | "restore" | "delete" }) =>
    action === "delete"
      ? apiRequest<unknown>(`${sectionsPath(yearId, stageId)}/${section.id}?version=${section.version}`, "DELETE")
      : apiRequest<unknown>(`${sectionsPath(yearId, stageId)}/${section.id}/${action}`, "POST", { version: section.version }));
}
