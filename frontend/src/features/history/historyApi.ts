import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import type { Paged } from "../academic-years/yearsApi";

/** Mirrors the backend AuditCategories (checked by AuditEventContractTests through the Arabic dictionary). */
export const auditCategories = ["timetable", "generation", "backup", "account", "settings", "import", "school"] as const;
export type AuditCategory = (typeof auditCategories)[number];

export type AuditParameters = Record<string, string | number | boolean>;

/** One history row: an event code and its parameters. The Arabic sentence is built by `auditSentence`. */
export type AuditEntry = {
  id: number;
  occurredAt: string;
  eventType: string;
  category: AuditCategory;
  target: string;
  params: AuditParameters | null;
};

export const auditKey = (category: AuditCategory | "", page: number, pageSize: number) => ["audit", category, page, pageSize] as const;

/** Newest first. Used by the history screen (filtered, paged) and the dashboard's recent-activity feed (first page). */
export function useAuditHistory(category: AuditCategory | "", page: number, pageSize: number) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (category) query.set("category", category);
  return useQuery({
    queryKey: auditKey(category, page, pageSize),
    queryFn: () => apiRequest<Paged<AuditEntry>>(`/api/v1/audit/?${query.toString()}`),
    placeholderData: keepPreviousData,
  });
}
