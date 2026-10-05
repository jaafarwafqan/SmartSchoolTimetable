import { useQuery } from "@tanstack/react-query";
import { apiRequest } from "../../api";

export type ReadinessFinding = {
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

export function useReadiness(yearId: number | undefined) {
  return useQuery({
    queryKey: readinessKey(yearId ?? 0),
    queryFn: () => apiRequest<ReadinessReport>(`/api/v1/academic-years/${yearId}/readiness`),
    enabled: yearId !== undefined,
  });
}