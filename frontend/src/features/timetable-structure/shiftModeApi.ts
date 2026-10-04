import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { useRefreshSchoolData } from "../../lib/schoolContext";
import { profileKey } from "../school-profile/profileApi";
import { shiftsKey, type Shift } from "./scheduleApi";

export type ShiftMode = "morning" | "evening" | "dual";
export type AffectedSection = { sectionId: number; stageName: string; label: string; shiftName: string; isArchived: boolean };
export type ShiftModeImpact = { mode: ShiftMode; allowed: boolean; shiftsToCreate: ("morning" | "evening")[]; shiftsToRemove: string[]; affectedSections: AffectedSection[] };

/** What applying a mode would create, remove and which sections block it (data only; the UI writes the Arabic). */
export function useShiftModeImpact(mode: ShiftMode | null) {
  return useQuery({
    queryKey: ["shift-mode-impact", mode],
    enabled: mode !== null,
    queryFn: () => apiRequest<ShiftModeImpact>(`/api/v1/shift-mode/impact?mode=${mode}`),
  });
}

export function useSetShiftMode() {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return useMutation({
    mutationFn: (input: { mode: ShiftMode; version: number }) =>
      apiRequest<{ mode: ShiftMode; shifts: Shift[]; profileVersion: number }>("/api/v1/shift-mode/", "PUT", input),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: shiftsKey }),
        queryClient.invalidateQueries({ queryKey: profileKey }),
        queryClient.invalidateQueries({ queryKey: ["shift-mode-impact"] }),
        queryClient.invalidateQueries({ queryKey: ["schedule-grid"] }),
        refreshSchoolData(),
      ]);
    },
  });
}
