import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { apiRequest } from "../api";
import { useRefreshBootstrap } from "../features/auth/useBootstrap";
import { logoutPath } from "../features/auth/useLogout";

const activityEvents = ["pointerdown", "keydown", "touchstart"] as const;

/** Client-side auto-lock. The server also expires the session independently. */
export function useInactivityLock(timeoutMinutes: number | null) {
  const refreshBootstrap = useRefreshBootstrap();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  useEffect(() => {
    if (timeoutMinutes === null) return;
    let timeout: number | undefined;
    const reset = () => {
      window.clearTimeout(timeout);
      timeout = window.setTimeout(() => {
        // A failed logout call is ignored: the server session expires on its own timer.
        void apiRequest<void>(logoutPath, "POST", {})
          .catch(() => undefined)
          .finally(() => {
            queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== "bootstrap" });
            void refreshBootstrap();
            navigate("/", { replace: true });
          });
      }, timeoutMinutes * 60_000);
    };
    activityEvents.forEach((event) => window.addEventListener(event, reset, { passive: true }));
    reset();
    return () => {
      window.clearTimeout(timeout);
      activityEvents.forEach((event) => window.removeEventListener(event, reset));
    };
  }, [timeoutMinutes, refreshBootstrap, navigate, queryClient]);
}
