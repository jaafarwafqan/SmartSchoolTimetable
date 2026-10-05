import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { refreshQueries } from "../../lib/refreshQueries";
import { useRefreshSchoolData } from "../../lib/schoolContext";
import type { Paged } from "../academic-years/yearsApi";

export type PeriodKind = "lesson" | "break";
export type BellTone = "classic" | "chime" | "beeps" | "soft";
export type Period = { position: number; kind: PeriodKind; startTime: string; endTime: string; startBell: boolean; endBell: boolean };
export type PeriodInput = Omit<Period, "position">;
export type ShiftKind = "morning" | "evening" | "other";
export type DayLessons = { day: number; lessons: number };
export type Shift = {
  id: number;
  academicYearId: number;
  name: string;
  displayOrder: number;
  kind: ShiftKind;
  lessonCount: number;
  periods: Period[];
  /** Lessons taught on each working day (per-day counts). */
  dayLessons: DayLessons[];
  weeklyLessons: number;
  version: number;
};
export type WorkingWeek = { days: number[]; weekStartDay: number; version: number };
export type BellSettings = { tone: BellTone; breakBell: boolean; version: number };
export type ShiftInput = { name: string; displayOrder: number; version: number };
export type ScheduleGrid = { days: number[]; lessonsPerDay: number; lessonsByDay: DayLessons[]; maxWeeklyLessons: number };
export type BlockedSlot = { day: number; lessonNumber: number };
export type GenerateInput = {
  firstStartTime: string;
  lessonMinutes: number;
  lessonCount: number;
  breakMinutes: number;
  breakAfterLesson: number | null;
  /** Several breaks (period presets); when set, the single break fields are ignored by the server. */
  breaks?: { afterLesson: number; minutes: number }[];
  /** Minutes between lessons without a break between them (ADR 0026). */
  gapMinutes?: number;
};

export const shiftsKey = ["shifts"] as const;
const gridKey = ["schedule-grid"] as const;
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

/** Working days × most lessons per day in the current year: the grid for blocked periods (2D, 2E). */
export function useScheduleGrid() {
  return useQuery({ queryKey: gridKey, queryFn: () => apiRequest<ScheduleGrid>("/api/v1/schedule-grid/") });
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
      // Timing changes move capacities: refresh the stage cards, sections and curriculum totals too.
      await Promise.all([refreshQueries(queryClient, [key, gridKey, ["stage-cards"], ["sections"], ["curriculum"]]), refreshSchoolData()]);
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

export type StageLessonsImpact = { stageId: number; stageName: string; day: number; stageLessons: number; shiftLessons: number };

export function useSaveDayLessons(yearId: number) {
  return useStructureMutation(({ shiftId, dayLessons, version, confirmStageChanges = false }: { shiftId: number; dayLessons: DayLessons[]; version: number; confirmStageChanges?: boolean }) =>
    apiRequest<Shift>(`${shiftsPath(yearId)}/${shiftId}/day-lessons`, "PUT", { dayLessons, version, confirmStageChanges }), shiftsKey);
}

/** Stages whose own count would be above the shortened shift (ADR 0027); nothing is saved. */
export function usePreviewDayLessons(yearId: number) {
  return useMutation({
    mutationFn: ({ shiftId, dayLessons, version }: { shiftId: number; dayLessons: DayLessons[]; version: number }) =>
      apiRequest<StageLessonsImpact[]>(`${shiftsPath(yearId)}/${shiftId}/day-lessons/impact`, "POST", { dayLessons, version }),
  });
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
