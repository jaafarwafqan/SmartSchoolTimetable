import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import type { ThemePreference } from "../../lib/theme";

export type PrintSetting = { paper: "a4" | "a3"; orientation: "portrait" | "landscape" };

/** The owner's stored preferences (M2). Digits stay on the school profile and the auto-lock on the owner account. */
export type Preferences = {
  theme: ThemePreference;
  /** 1 or 2: the viewer always opens on that semester; null follows the year's dates. */
  defaultSemester: 1 | 2 | null;
  section: PrintSetting;
  teacher: PrintSetting;
  school: PrintSetting;
  printFit: boolean;
  version: number;
};

export type PreferencesInput = Omit<Preferences, "version"> & { version: number };

export const preferencesKey = ["preferences"] as const;
const preferencesPath = "/api/v1/preferences";

export function usePreferences() {
  return useQuery({ queryKey: preferencesKey, queryFn: () => apiRequest<Preferences>(`${preferencesPath}/`) });
}

export function useSavePreferences() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: PreferencesInput) => apiRequest<Preferences>(`${preferencesPath}/`, "PUT", input),
    onSuccess: (saved) => {
      client.setQueryData(preferencesKey, saved);
      void client.invalidateQueries({ queryKey: ["audit"] });
    },
  });
}
