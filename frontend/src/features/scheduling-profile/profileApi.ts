import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { refreshQueries } from "../../lib/refreshQueries";

export const ruleKeys = ["spreadSubjectsAcrossDays", "avoidTeacherGaps", "heavySubjectsEarly", "avoidSameSubjectRepeated", "keepDoubleLessonsTogether"] as const;
export type RuleKey = (typeof ruleKeys)[number];

export type SchedulingRule = { key: RuleKey; enabled: boolean; weight: number; enabledByDefault: boolean; defaultWeight: number };
export type SchedulingProfile = { rules: SchedulingRule[]; profileVersion: number; isDefault: boolean; version: number };
export type RuleInput = { key: RuleKey; enabled: boolean; weight: number };

const profileKey = ["scheduling-profile"] as const;
const profilePath = "/api/v1/scheduling-profile";

export function useSchedulingProfile() {
  return useQuery({ queryKey: profileKey, queryFn: () => apiRequest<SchedulingProfile>(`${profilePath}/`) });
}

function useProfileMutation<TInput>(request: (input: TInput) => Promise<SchedulingProfile>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => { await refreshQueries(queryClient, [profileKey]); },
  });
}

export function useSaveProfile() {
  return useProfileMutation((input: { rules: RuleInput[]; version: number }) => apiRequest<SchedulingProfile>(`${profilePath}/`, "PUT", input));
}

export function useRestoreDefaults() {
  return useProfileMutation((version: number) => apiRequest<SchedulingProfile>(`${profilePath}/restore-defaults`, "POST", { confirm: true, version }));
}

/** Weight choices in steps of five (choose, don't type), plus the saved value when it is not a multiple of five. */
export function weightChoices(current: number): number[] {
  const steps = Array.from({ length: 21 }, (_, index) => index * 5);
  return steps.includes(current) ? steps : [...steps, current].sort((a, b) => a - b);
}
