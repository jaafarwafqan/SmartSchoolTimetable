import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo } from "react";
import { apiRequest } from "../api";
import { createFormatter, defaultDisplay, type CalendarPreference, type NumeralPreference } from "./format";

export type CurrentPeriod = { id: number; name: string; startDate: string; endDate: string };

/** GET /api/v1/school-context: school name, current year/term and display preferences (server state). */
export type SchoolContext = {
  schoolName: string;
  numeralSystem: NumeralPreference;
  calendarDisplay: CalendarPreference;
  timeZone: string;
  currentYear: CurrentPeriod | null;
  currentTerm: CurrentPeriod | null;
};

export const schoolContextKey = ["school-context"] as const;

export function useSchoolContext(enabled = true) {
  return useQuery({
    queryKey: schoolContextKey,
    queryFn: () => apiRequest<SchoolContext>("/api/v1/school-context"),
    enabled,
  });
}

/** Formatting helper bound to the school's numerals, calendar and time zone. */
export function useFormatter() {
  const { data } = useSchoolContext();
  return useMemo(() => createFormatter(data ? {
    numeralSystem: data.numeralSystem,
    calendarDisplay: data.calendarDisplay,
    timeZone: data.timeZone,
  } : defaultDisplay), [data]);
}

/** Refreshes everything that depends on school-wide data (shell, dashboard). */
export function useRefreshSchoolData() {
  const queryClient = useQueryClient();
  return useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: schoolContextKey }),
      queryClient.invalidateQueries({ queryKey: ["dashboard"] }),
    ]);
  }, [queryClient]);
}
