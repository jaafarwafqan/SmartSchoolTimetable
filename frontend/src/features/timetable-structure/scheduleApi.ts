import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { useRefreshSchoolData } from "../../lib/schoolContext";
import type { Paged } from "../academic-years/yearsApi";

export type PeriodKind = "lesson" | "break";
export type BellTone = "classic" | "chime" | "beeps" | "soft";
export type Period = { position: number; kind: PeriodKind; startTime: string; endTime: string; startBell: boolean; endBell: boolean };
export type PeriodInput = Omit<Period, "position">;
export type Shift = { id: number; academicYearId: number; name: string; displayOrder: number; lessonCount: number; periods: Period[]; version: number };
export type WorkingWeek = { days: number[]; weekStartDay: number; version: number };
export type BellSettings = { tone: BellTone; breakBell: boolean; version: number };
export type ShiftInput = { name: string; displayOrder: number; version: number };
export type GenerateInput = { firstStartTime: string; lessonMinutes: number; lessonCount: number; breakMinutes: number; breakAfterLesson: number | null };

export const shiftsKey = ["shifts"] as const;
const weekKey = ["working-week"] as const;
const bellKey = ["bell-settings"] as const;
const shiftsPath = (yearId: number) => `/api/v1/academic-years/${yearId}/shifts`;

export function useShifts(yearId: number | null) {
  return useQuery({
    queryKey: [...shiftsKey, yearId],
    enabled: yearId !== null,
    queryFn: () => apiRequest<Paged<Shift>>(`${shiftsPath(yearId ?? 0)}/?pageSize=100`),
  });
}

export function useWorkingWeek() {
  return useQuery({ queryKey: weekKey, queryFn: () => apiRequest<WorkingWeek>("/api/v1/working-days/") });
}

export function useBellSettings() {
  return useQuery({ queryKey: bellKey, queryFn: () => apiRequest<BellSettings>("/api/v1/bell-settings/") });
}

/** Mutation that refreshes the given lists and the dashboard (counts and checklist) on success. */
function useStructureMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>, key: readonly unknown[]) {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([queryClient.invalidateQueries({ queryKey: key }), refreshSchoolData()]);
    },
  });
}

export function useSaveShift(yearId: number) {
  return useStructureMutation(({ id, input }: { id: number | null; input: ShiftInput }) =>
    id === null
      ? apiRequest<Shift>(`${shiftsPath(yearId)}/`, "POST", input)
      : apiRequest<Shift>(`${shiftsPath(yearId)}/${id}`, "PUT", input), shiftsKey);
}

export function useDeleteShift(yearId: number) {
  return useStructureMutation(({ id, version }: { id: number; version: number }) =>
    apiRequest<void>(`${shiftsPath(yearId)}/${id}?version=${version}`, "DELETE"), shiftsKey);
}

export function useSavePeriods(yearId: number) {
  return useStructureMutation(({ shiftId, periods, version }: { shiftId: number; periods: PeriodInput[]; version: number }) =>
    apiRequest<Shift>(`${shiftsPath(yearId)}/${shiftId}/periods`, "PUT", { periods, version }), shiftsKey);
}

export function useGeneratePeriods(yearId: number) {
  return useMutation({
    mutationFn: (input: GenerateInput) =>
      apiRequest<{ periods: Period[] }>(`${shiftsPath(yearId)}/generate-periods`, "POST", input),
  });
}

export function useSaveWeek() {
  return useStructureMutation((input: WorkingWeek) => apiRequest<WorkingWeek>("/api/v1/working-days/", "PUT", input), weekKey);
}

export function useSaveBell() {
  return useStructureMutation((input: BellSettings) => apiRequest<BellSettings>("/api/v1/bell-settings/", "PUT", input), bellKey);
}
