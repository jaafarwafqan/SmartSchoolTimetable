import { House, LogOut, Settings as SettingsIcon } from "lucide-react";
import { useEffect } from "react";
import { Link, Route, Routes, useNavigate } from "react-router-dom";
import { apiRequest } from "../api";
import { AlertMessage } from "../components/AlertMessage";
import { Button } from "../components/ui/button";
import { useRefreshBootstrap } from "../features/auth/useBootstrap";
import { logoutPath, useLogout } from "../features/auth/useLogout";
import { HomePage } from "../features/home/HomePage";
import { SettingsScreen } from "../features/settings/SettingsScreen";
import { messages } from "../i18n/messages";
import type { Bootstrap } from "../lib/bootstrapQuery";
import { useErrorText } from "../lib/useErrorText";
import { NotFoundPage } from "./NotFoundPage";

const activityEvents = ["pointerdown", "keydown", "touchstart"] as const;

/** Client-side auto-lock. The server also expires the session independently. */
function useInactivityLock(timeoutMinutes: number | null) {
  const refreshBootstrap = useRefreshBootstrap();
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
  }, [timeoutMinutes, refreshBootstrap, navigate]);
}

export function AppShell({ bootstrap }: { bootstrap: Bootstrap }) {
  const { error, setError, showError } = useErrorText();
  const logout = useLogout(showError);
  useInactivityLock(bootstrap.inactivityTimeoutMinutes);

  return (
    <main className="app-shell">
      <header className="app-header">
        <Link className="brand-link" to="/" aria-label={messages.app.home}>
          <House aria-hidden="true" size={22} strokeWidth={2} />
          <span>{messages.app.brand}</span>
        </Link>
        <nav aria-label={messages.app.settings}>
          <Link className="nav-link" to="/settings">
            <SettingsIcon aria-hidden="true" size={20} strokeWidth={2} />
            <span>{messages.app.settings}</span>
          </Link>
          <Button
            variant="secondary"
            icon={<LogOut aria-hidden="true" size={20} />}
            disabled={logout.isPending}
            onClick={() => {
              setError(null);
              logout.mutate();
            }}
          >
            {messages.app.logout}
          </Button>
        </nav>
      </header>
      <AlertMessage message={error} />
      <Routes>
        <Route path="/" element={<HomePage username={bootstrap.username} />} />
        <Route path="/settings" element={<SettingsScreen bootstrap={bootstrap} />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </main>
  );
}
