import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { useRefreshSchoolData } from "../../lib/schoolContext";
import { profileKey } from "../school-profile/profileApi";
import { shiftsKey } from "./scheduleApi";
import { sessionPlanKey } from "./sessionPlanApi";
import type { ShiftSystem, ShiftSystemCommand } from "./shiftSystem";

export type LegacyShift = { id: number; name: string; kind: string; sections: number };

/** The school's system of work; `legacy` means the current year still has the old two-shift layout (data only). */
export type ShiftSystemState = { system: ShiftSystem; legacy: boolean; legacyShifts: LegacyShift[]; shiftId: number | null; profileVersion: number };

export const shiftSystemKey = ["shift-system"] as const;

export function useShiftSystem() {
  return useQuery({ queryKey: shiftSystemKey, queryFn: () => apiRequest<ShiftSystemState>("/api/v1/shift-system/") });
}

/** Everything that depends on the shift layout is reloaded after a change. */
function useRefreshAfterShiftChange() {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: shiftSystemKey }),
      queryClient.invalidateQueries({ queryKey: shiftsKey }),
      queryClient.invalidateQueries({ queryKey: sessionPlanKey }),
      queryClient.invalidateQueries({ queryKey: profileKey }),
      queryClient.invalidateQueries({ queryKey: ["schedule-grid"] }),
      queryClient.invalidateQueries({ queryKey: ["stage-cards"] }),
      queryClient.invalidateQueries({ queryKey: ["curriculum"] }),
      queryClient.invalidateQueries({ queryKey: ["readiness"] }),
      queryClient.invalidateQueries({ queryKey: ["timetable"] }),
      queryClient.invalidateQueries({ queryKey: ["audit"] }),
      refreshSchoolData(),
    ]);
  };
}

export function useSaveShiftSystem() {
  const refresh = useRefreshAfterShiftChange();
  return useMutation({
    mutationFn: (command: ShiftSystemCommand) => apiRequest<ShiftSystemState>("/api/v1/shift-system/", "PUT", command),
    onSuccess: refresh,
  });
}

/** One-time guided conversion of the old two-shift layout (the server backs up first). */
export function useConvertLegacy() {
  const refresh = useRefreshAfterShiftChange();
  return useMutation({
    mutationFn: (targetSystem: ShiftSystem) =>
      apiRequest<{ automaticBackupPath: string; system: ShiftSystemState }>("/api/v1/shift-system/convert", "POST", { targetSystem }),
    onSuccess: refresh,
  });
}
