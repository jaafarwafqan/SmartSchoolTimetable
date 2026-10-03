import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { apiRequest } from "../../api";
import { useRefreshBootstrap } from "./useBootstrap";

export const logoutPath = "/api/v1/auth/logout";

export function useLogout(onError: (reason: unknown) => void) {
  const refreshBootstrap = useRefreshBootstrap();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: () => apiRequest<void>(logoutPath, "POST", {}),
    onSuccess: async () => {
      await refreshBootstrap();
      navigate("/", { replace: true });
    },
    onError,
  });
}
