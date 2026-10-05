import type { QueryClient, QueryKey } from "@tanstack/react-query";

/**
 * Refreshes queries after a write. An in-flight first load (no data yet) is reused by `invalidateQueries`, so a read
 * that started before the write would land afterwards with stale data; cancelling first forces a fresh request.
 */
export async function refreshQueries(queryClient: QueryClient, keys: readonly QueryKey[]): Promise<void> {
  await Promise.all(keys.map((queryKey) => queryClient.cancelQueries({ queryKey })));
  await Promise.all(keys.map((queryKey) => queryClient.invalidateQueries({ queryKey })));
}
