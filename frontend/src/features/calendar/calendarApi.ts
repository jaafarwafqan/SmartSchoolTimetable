import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import type { Paged } from "../academic-years/yearsApi";

export type CalendarKind = "officialHoliday" | "schoolHoliday" | "exam" | "specialDay";
export type CalendarDay = {
  id: number;
  title: string;
  startDate: string;
  endDate: string;
  kind: CalendarKind;
  affectsSchedule: boolean;
  outsideCurrentYear: boolean;
  /** MF8: typed by the owner, or added from the Iraqi official-holidays template. */
  source: "manual" | "iraqTemplate";
  /** MF8: a Hijri date calculated with Umm al-Qura; the official announcement may differ. */
  isApproximate: boolean;
  /** MF8: a disabled entry stays in the list but is not counted as a holiday. */
  isEnabled: boolean;
  version: number;
};
export type IraqHolidayPreview = { key: string; title: string; startDate: string; endDate: string; approximate: boolean; alreadyAdded: boolean };
export type IraqHolidayPreviewResponse = { yearLabel: string; holidays: IraqHolidayPreview[] };
export type ImportIraqHolidaysResult = { added: number; skipped: number };
export type CalendarDayInput = { title: string; startDate: string; endDate: string | null; kind: CalendarKind; affectsSchedule: boolean; version: number };

const calendarKey = ["calendar-days"] as const;
const calendarPath = "/api/v1/calendar-days";

/** List (search + paging) or a date window for the month view. */
export function useCalendarDays(params: { search?: string; page?: number; from?: string; to?: string }) {
  const query = new URLSearchParams({ page: String(params.page ?? 1), pageSize: "100" });
  if (params.search?.trim()) query.set("search", params.search.trim());
  if (params.from) query.set("from", params.from);
  if (params.to) query.set("to", params.to);
  return useQuery({
    queryKey: [...calendarKey, params],
    queryFn: () => apiRequest<Paged<CalendarDay>>(`${calendarPath}/?${query.toString()}`),
    placeholderData: keepPreviousData,
  });
}

function useCalendarMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: calendarKey }); },
  });
}

export function useSaveCalendarDay() {
  return useCalendarMutation(({ id, input }: { id: number | null; input: CalendarDayInput }) =>
    id === null
      ? apiRequest<CalendarDay>(`${calendarPath}/`, "POST", input)
      : apiRequest<CalendarDay>(`${calendarPath}/${id}`, "PUT", input));
}

export function useSetCalendarDayEnabled() {
  return useCalendarMutation((input: { day: CalendarDay; enabled: boolean }) =>
    apiRequest<CalendarDay>(`${calendarPath}/${input.day.id}/enabled`, "PUT", { enabled: input.enabled, version: input.day.version }));
}

/** The template's holidays inside an academic year (nothing is saved). */
export function useIraqHolidayPreview(yearId: number | null) {
  return useQuery({
    queryKey: [...calendarKey, "iraq-holidays", yearId],
    queryFn: () => apiRequest<IraqHolidayPreviewResponse>(`${calendarPath}/iraq-holidays?yearId=${yearId}`),
    enabled: yearId !== null,
    refetchOnMount: "always",
  });
}

export function useImportIraqHolidays() {
  return useCalendarMutation((yearId: number) => apiRequest<ImportIraqHolidaysResult>(`${calendarPath}/iraq-holidays`, "POST", { yearId }));
}

export function useDeleteCalendarDay() {
  return useCalendarMutation((day: CalendarDay) => apiRequest<unknown>(`${calendarPath}/${day.id}?version=${day.version}`, "DELETE"));
}
