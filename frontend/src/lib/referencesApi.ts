import { useQuery } from "@tanstack/react-query";
import { apiRequest } from "../api";

/** Record kinds the reference guard answers for (GET /api/v1/references/{kind}/{id}). */
export type ReferenceKind = "subject" | "teacher" | "section" | "stage" | "shift" | "resource" | "curriculumEntry";

export type DependentGroup = {
  kind: "section" | "curriculumEntry";
  active: number;
  archived: number;
  /** A few dependent names (user data, shown as written). */
  samples: string[];
  errorCode: string;
};

export type ReferenceReport = {
  kind: ReferenceKind;
  id: number;
  dependents: DependentGroup[];
  /** The error code archiving returns (null: archive allowed). */
  archiveBlockedBy: string | null;
  /** The error code deleting returns (null: delete allowed). */
  deleteBlockedBy: string | null;
};

export const referencesKey = ["references"] as const;

/** What depends on a record; always fetched fresh because any edit elsewhere may change it. */
export function useReferences(kind: ReferenceKind, id: number | null) {
  return useQuery({
    queryKey: [...referencesKey, kind, id],
    queryFn: () => apiRequest<ReferenceReport>(`/api/v1/references/${kind}/${id ?? 0}`),
    enabled: id !== null,
    staleTime: 0,
    gcTime: 0,
  });
}
