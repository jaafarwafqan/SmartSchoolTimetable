import { GraduationCap, House, LogOut, Settings as SettingsIcon, UserRound } from "lucide-react";
import { Alert } from "../components/ui/alert";
import { useEffect } from "react";
import { Link, NavLink, Route, Routes, useNavigate } from "react-router-dom";
import { apiRequest } from "../api";
import { Button } from "../components/ui/button";
import { useRefreshBootstrap } from "../features/auth/useBootstrap";
import { logoutPath, useLogout } from "../features/auth/useLogout";
import { HomePage } from "../features/home/HomePage";
import { SettingsScreen } from "../features/settings/SettingsScreen";
import { messages } from "../i18n/messages";
import type { Bootstrap } from "../lib/bootstrapQuery";
import { useFormFeedback } from "../lib/useFormFeedback";
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

const navClass = ({ isActive }: { isActive: boolean }) => `nav-link${isActive ? " is-active" : ""}`;

export function AppShell({ bootstrap }: { bootstrap: Bootstrap }) {
  const feedback = useFormFeedback();
  const logout = useLogout(feedback.showError);
  useInactivityLock(bootstrap.inactivityTimeoutMinutes);

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header-inner">
          <Link className="brand-link" to="/" aria-label={messages.app.home}>
            <span className="brand-logo brand-logo-sm" aria-hidden="true">
              <GraduationCap size={20} strokeWidth={2} />
            </span>
            <span>{messages.app.brand}</span>
          </Link>
          <nav className="main-nav" aria-label={messages.app.mainNavigation}>
            <NavLink className={navClass} to="/" end>
              <House aria-hidden="true" size={18} strokeWidth={2} />
              <span>{messages.app.home}</span>
            </NavLink>
            <NavLink className={navClass} to="/settings">
              <SettingsIcon aria-hidden="true" size={18} strokeWidth={2} />
              <span>{messages.app.settings}</span>
            </NavLink>
          </nav>
          <div className="header-actions">
            <span className="user-chip" title={messages.app.signedInAs}>
              <UserRound aria-hidden="true" size={16} strokeWidth={2} />
              <span>{bootstrap.username}</span>
            </span>
            <Button
              variant="secondary"
              icon={<LogOut aria-hidden="true" size={18} />}
              loading={logout.isPending}
              onClick={() => {
                feedback.reset();
                logout.mutate();
              }}
            >
              {messages.app.logout}
            </Button>
          </div>
        </div>
      </header>
      <main className="app-main">
        <Alert tone="error" message={feedback.error} />
        <Routes>
          <Route path="/" element={<HomePage username={bootstrap.username} />} />
          <Route path="/settings" element={<SettingsScreen bootstrap={bootstrap} />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
    </div>
  );
}
