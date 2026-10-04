import { useQuery } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import type { CurriculumStage } from "../curriculum/curriculumApi";

export type DashboardSummary = {
  counts: Array<{ key: string; value: number }>;
  checklist: Array<{ key: string; done: boolean }>;
  /** Planned lessons of each stage of the current year against capacity, per shift. */
  curriculum: CurriculumStage[];
  setupFinished: boolean;
};

export const dashboardKey = ["dashboard"] as const;

export function useDashboard() {
  return useQuery({
    queryKey: dashboardKey,
    queryFn: () => apiRequest<DashboardSummary>("/api/v1/dashboard-summary"),
  });
}
