import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";
import { apiRequest } from "../../api";
import { bootstrapPath, bootstrapQueryKey, type Bootstrap } from "../../lib/bootstrapQuery";

export function useBootstrap() {
  return useQuery({
    queryKey: bootstrapQueryKey,
    queryFn: () => apiRequest<Bootstrap>(bootstrapPath),
  });
}

export function useRefreshBootstrap() {
  const queryClient = useQueryClient();
  return useCallback(
    () => queryClient.invalidateQueries({ queryKey: bootstrapQueryKey }),
    [queryClient],
  );
}
