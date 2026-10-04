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
  version: number;
};
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

export function useDeleteCalendarDay() {
  return useCalendarMutation((day: CalendarDay) => apiRequest<unknown>(`${calendarPath}/${day.id}?version=${day.version}`, "DELETE"));
}
