import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import type { SessionDay, SessionKind, SessionPlan, SessionSystem } from "./sessionPlan";
import type { PeriodInput } from "./scheduleApi";

export const sessionPlanKey = ["session-plan"] as const;

/** R3 «نظام الدوام اليومي» of the current year (data only; the UI writes the Arabic). */
export function useSessionPlan() {
  return useQuery({ queryKey: sessionPlanKey, queryFn: () => apiRequest<SessionPlan>("/api/v1/session-plan/") });
}

export type SaveSessionPlanInput = {
  system: SessionSystem;
  timings: { session: SessionKind; periods: Omit<PeriodInput, "startBell" | "endBell">[] }[];
  days: SessionDay[];
  version: number;
};

export function useSaveSessionPlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: SaveSessionPlanInput) => apiRequest<SessionPlan>("/api/v1/session-plan/", "PUT", input),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: sessionPlanKey }),
        queryClient.invalidateQueries({ queryKey: ["timetable"] }),
        queryClient.invalidateQueries({ queryKey: ["readiness"] }),
      ]);
    },
  });
}
