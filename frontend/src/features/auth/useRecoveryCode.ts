import { useMutation } from "@tanstack/react-query";
import { useCallback } from "react";
import { apiRequest } from "../../api";
import { useUiStore } from "../../state/session";
import { useRefreshBootstrap } from "./useBootstrap";

export type RecoveryCodeResponse = { recoveryCode: string };

/** Keeps a newly issued recovery code in memory and refreshes the bootstrap state that gates the UI. */
export function useStoreIssuedRecoveryCode() {
  const setRecoveryCode = useUiStore((state) => state.setRecoveryCode);
  const refreshBootstrap = useRefreshBootstrap();
  return useCallback(
    async (recoveryCode: string) => {
      setRecoveryCode(recoveryCode);
      await refreshBootstrap();
    },
    [refreshBootstrap, setRecoveryCode],
  );
}

export function useRegenerateRecoveryCode(onError: (reason: unknown) => void) {
  const storeIssuedCode = useStoreIssuedRecoveryCode();
  return useMutation({
    mutationFn: (currentPassword: string) =>
      apiRequest<RecoveryCodeResponse>("/api/v1/auth/recovery-code/regenerate", "POST", { currentPassword }),
    onSuccess: (result) => storeIssuedCode(result.recoveryCode),
    onError,
  });
}

export function useAcknowledgeRecoveryCode() {
  const setRecoveryCode = useUiStore((state) => state.setRecoveryCode);
  const refreshBootstrap = useRefreshBootstrap();
  return useCallback(async () => {
    await apiRequest<void>("/api/v1/auth/recovery-code/acknowledge", "POST", {});
    setRecoveryCode(null);
    await refreshBootstrap();
  }, [refreshBootstrap, setRecoveryCode]);
}
