import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { refreshQueries } from "../../lib/refreshQueries";
import type { BlockedSlot } from "./scheduleApi";

export type OrphanOwner = { kind: "teacher" | "subject"; id: number; name: string; version: number; periods: BlockedSlot[] };
export type OrphanReport = { owners: OrphanOwner[]; total: number };

/** Under the grid key: every timing change (days, shifts, lessons per day) refreshes the orphan report too. */
export const orphansKey = ["schedule-grid", "orphans"] as const;
const orphansPath = "/api/v1/blocked-periods/orphans";

/** Blocked periods outside the current grid; read fresh on every screen that shows the notice. */
export function useOrphanBlockedPeriods() {
  return useQuery({ queryKey: orphansKey, queryFn: () => apiRequest<OrphanReport>(`${orphansPath}/`), staleTime: 0 });
}

/** Removes the orphans of exactly the owners shown in the preview (their versions are checked). */
export function useCleanOrphans() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (owners: OrphanOwner[]) => apiRequest<OrphanReport>(`${orphansPath}/clean`, "POST", {
      owners: owners.map((owner) => ({ kind: owner.kind, id: owner.id, version: owner.version })),
    }),
    onSuccess: async () => { await refreshQueries(queryClient, [orphansKey, ["teachers"], ["subjects"]]); },
  });
}
