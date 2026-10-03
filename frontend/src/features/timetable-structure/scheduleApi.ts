import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";

export type Period = { position: number; kind: "lesson" | "break"; startTime: string; endTime: string; startBell: boolean; endBell: boolean };
export type Shift = { id: number; academicYearId: number; name: string; displayOrder: number; lessonCount: number; periods: Period[]; version: number };
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };
export type WorkingWeek = { days: number[]; weekStartDay: number; version: number };
export type BellSettings = { tone: "Classic" | "Chime" | "Beeps" | "Soft"; breakBell: boolean; version: number };
const keys = { shifts: ["shifts"] as const, week: ["working-week"] as const, bell: ["bell-settings"] as const };

export function useShifts(yearId: number | undefined) {
  return useQuery({ queryKey: [...keys.shifts, yearId], enabled: yearId !== undefined, queryFn: () => apiRequest<Page<Shift>>(`/api/v1/academic-years/${yearId}/shifts/?pageSize=100`) });
}
export function useWorkingWeek() { return useQuery({ queryKey: keys.week, queryFn: () => apiRequest<WorkingWeek>("/api/v1/working-days/") }); }
export function useBellSettings() { return useQuery({ queryKey: keys.bell, queryFn: () => apiRequest<BellSettings>("/api/v1/bell-settings/") }); }
function useSave<T>(path: string, method: "PUT" | "POST", invalidate: readonly (readonly unknown[])[]) {
  const client = useQueryClient();
  return useMutation({ mutationFn: (body: T) => apiRequest(path, method, body), onSuccess: async () => { await Promise.all(invalidate.map(queryKey => client.invalidateQueries({ queryKey }))); } });
}
export function useCreateShift(yearId: number) { return useSave<{ name: string; displayOrder: number; version: number }>(`/api/v1/academic-years/${yearId}/shifts/`, "POST", [keys.shifts]); }
export function useUpdateWeek() { return useSave<{ days: number[]; weekStartDay: number; version: number }>("/api/v1/working-days/", "PUT", [keys.week]); }
export function useUpdateBell() { return useSave<{ tone: BellSettings["tone"]; breakBell: boolean; version: number }>("/api/v1/bell-settings/", "PUT", [keys.bell]); }
export function useSavePeriods(yearId: number, shift: Shift) {
  return useSave<{ periods: Omit<Period, "position">[]; version: number }>(`/api/v1/academic-years/${yearId}/shifts/${shift.id}/periods`, "PUT", [keys.shifts]);
}
export async function generatePeriods(yearId: number, input: { firstStartTime: string; lessonMinutes: number; lessonCount: number; breakMinutes: number; breakAfterLesson: number | null }) {
  return apiRequest<{ periods: Period[] }>(`/api/v1/academic-years/${yearId}/shifts/generate-periods`, "POST", input);
}
