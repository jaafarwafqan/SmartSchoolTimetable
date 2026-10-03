import { useQuery } from "@tanstack/react-query";
import { apiRequest } from "../../api";

export type DashboardSummary = {
  counts: Array<{ key: string; value: number }>;
  checklist: Array<{ key: string; done: boolean }>;
};

export const dashboardKey = ["dashboard"] as const;

export function useDashboard() {
  return useQuery({
    queryKey: dashboardKey,
    queryFn: () => apiRequest<DashboardSummary>("/api/v1/dashboard-summary"),
  });
}
