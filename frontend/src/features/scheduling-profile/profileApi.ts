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

/**
 * MF9: the owner chooses how much each rule matters in three words; the server keeps its 0–100 weights (ADR 0045).
 * «غير مهم» switches the rule off, «مهم» weighs 20 and «مهم جداً» 40. A saved weight is shown as the nearest level
 * (below 10 or off: «غير مهم»; 30 and above: «مهم جداً»; otherwise «مهم»), so the defaults (20, 30, 15, 25, 10) read
 * naturally, and a rule is rewritten only when the owner changes its level.
 */
export const priorityLevels = ["notImportant", "important", "veryImportant"] as const;
export type PriorityLevel = (typeof priorityLevels)[number];

export const levelWeights: Record<PriorityLevel, number> = { notImportant: 0, important: 20, veryImportant: 40 };
const importantFrom = 10;
const veryImportantFrom = 30;

export function levelOf(rule: { enabled: boolean; weight: number }): PriorityLevel {
  if (!rule.enabled || rule.weight < importantFrom) return "notImportant";
  return rule.weight >= veryImportantFrom ? "veryImportant" : "important";
}

/** The rule as saved after choosing `level`; the same level keeps the exact saved weight. */
export function withLevel(rule: RuleInput, level: PriorityLevel): RuleInput {
  if (levelOf(rule) === level) return rule;
  return { key: rule.key, enabled: level !== "notImportant", weight: levelWeights[level] };
}
