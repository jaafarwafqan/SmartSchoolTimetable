import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { refreshQueries } from "../../lib/refreshQueries";

export const ruleKeys = [
  "spreadSubjectsAcrossDays", "spreadTeacherLessons", "avoidTeacherGaps", "balanceTeacherDays", "fairLastPeriods",
  "heavySubjectsEarly", "avoidSameSubjectRepeated", "keepDoubleLessonsTogether",
] as const;
export type RuleKey = (typeof ruleKeys)[number];

export type SchedulingRule = { key: RuleKey; enabled: boolean; weight: number; enabledByDefault: boolean; defaultWeight: number };
export type SchedulingProfile = {
  rules: SchedulingRule[];
  profileVersion: number;
  isDefault: boolean;
  version: number;
  /** The school's «أقصى حصص متتالية للمعلم»: 2 or 3. */
  defaultMaxConsecutive: number;
  /** «جعل التوازن إلزامياً»: balanced teacher days as a hard rule. */
  balanceHard: boolean;
};
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
  return useProfileMutation((input: { rules: RuleInput[]; version: number; defaultMaxConsecutive: number; balanceHard: boolean }) => apiRequest<SchedulingProfile>(`${profilePath}/`, "PUT", input));
}

export function useRestoreDefaults() {
  return useProfileMutation((version: number) => apiRequest<SchedulingProfile>(`${profilePath}/restore-defaults`, "POST", { confirm: true, version }));
}

/** Two forms of the teacher's day that exclude each other: choosing one switches the other off. */
export const exclusiveRules: Partial<Record<RuleKey, RuleKey>> = { spreadTeacherLessons: "avoidTeacherGaps", avoidTeacherGaps: "spreadTeacherLessons" };

/** The rules after `key` got `level`: the rule it excludes is switched off when `key` is switched on. */
export function applyLevel(rules: readonly RuleInput[], key: RuleKey, level: PriorityLevel): RuleInput[] {
  const other = exclusiveRules[key];
  return rules.map((rule) => {
    if (rule.key === key) return withLevel(rule, level);
    return other && rule.key === other && level !== "notImportant" ? withLevel(rule, "notImportant") : rule;
  });
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
