import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { apiRequest } from "../../api";
import { messages } from "../../i18n/messages";
import { useUiStore } from "../../state/session";
import { useRefreshBootstrap } from "./useBootstrap";

export const logoutPath = "/api/v1/auth/logout";

/** Ends the session and clears cached school data so nothing protected stays in memory. */
function useEndSession(onDone: () => void, onError: (reason: unknown) => void) {
  const refreshBootstrap = useRefreshBootstrap();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: () => apiRequest<void>(logoutPath, "POST", {}),
    onSuccess: async () => {
      onDone();
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== "bootstrap" });
      await refreshBootstrap();
      navigate("/", { replace: true });
    },
    onError,
  });
}

export function useLogout(onError: (reason: unknown) => void) {
  const setLockedUsername = useUiStore((state) => state.setLockedUsername);
  return useEndSession(() => setLockedUsername(null), onError);
}

/** "Lock screen": ends the session but keeps the username so the login form opens pre-filled. */
export function useLock(username: string | null, onError: (reason: unknown) => void) {
  const setLockedUsername = useUiStore((state) => state.setLockedUsername);
  const setLoginNotice = useUiStore((state) => state.setLoginNotice);
  return useEndSession(() => {
    setLockedUsername(username);
    setLoginNotice(messages.school.shell.lockedNotice);
  }, onError);
}
