import { useQuery } from "@tanstack/react-query";
import { apiRequest } from "../../api";

/** Every pre-solve finding code (mirrors the backend FindingCodes.All; checked by FindingCodeContractTests). */
export const findingCodes = [
  "NOTHING_TO_SCHEDULE",
  "UNASSIGNED_LINES",
  "SECTION_OVER_CAPACITY",
  "SECTION_UNDER_CAPACITY",
  "TEACHER_OVERLOAD",
  "TEACHER_RELEASED_ASSIGNED",
  "TEACHER_ARCHIVED_ASSIGNED",
  "TEACHER_PARTIAL_RELEASE",
  "SUBJECT_SLOTS_SHORT",
  "ASSIGNMENT_INFEASIBLE",
  "RESOURCE_OVER_CAPACITY",
  "RESOURCE_ARCHIVED",
  "DOUBLE_PERIOD_IMPOSSIBLE",
  "DOUBLE_PERIOD_TIGHT",
  "ORPHAN_BLOCKED_PERIODS",
  "SHIFT_WITHOUT_PERIODS",
  "DISTRIBUTION_DISABLED_IN_CURRICULUM",
  "STAGE_WITHOUT_CURRICULUM",
  "TEACHER_SHIFT_OVERLAP",
] as const;
export type FindingCode = (typeof findingCodes)[number];

export function isFindingCode(code: string): code is FindingCode {
  return (findingCodes as readonly string[]).includes(code);
}

export type ReadinessFinding = {
  /** A known `FindingCode`; any other value is shown with the generic Arabic message. */
  code: string;
  severity: "error" | "warning";
  entity: { kind: string; id: number; name: string };
  related: Array<{ kind: string; id: number; name: string }>;
  required: number | null;
  available: number | null;
  shortage: number | null;
  details: string[];
  fixes: string[];
};

export type ReadinessReport = {
  ready: boolean;
  errors: number;
  warnings: number;
  findings: ReadinessFinding[];
  inputHash: string;
  checkedAt: string;
  sections: number;
  lines: number;
  assigned: number;
  teachers: number;
};

export const readinessKey = (yearId: number) => ["dashboard", "readiness", yearId] as const;

/** `doublePeriods`: check for the «دروس مزدوجة» mode, where an impossible double period is an error (ADR 0035). */
export function useReadiness(yearId: number | undefined, doublePeriods = false) {
  return useQuery({
    queryKey: [...readinessKey(yearId ?? 0), doublePeriods],
    queryFn: () => apiRequest<ReadinessReport>(`/api/v1/academic-years/${yearId}/readiness/${doublePeriods ? "?doublePeriods=true" : ""}`),
    enabled: yearId !== undefined,
  });
}