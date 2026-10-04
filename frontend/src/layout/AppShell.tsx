import { useCallback, useRef, useState } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { Alert } from "../components/ui/alert";
import { AcademicYearsPage } from "../features/academic-years/AcademicYearsPage";
import { CalendarPage } from "../features/calendar/CalendarPage";
import { DashboardPage } from "../features/dashboard/DashboardPage";
import { SchoolProfilePage } from "../features/school-profile/SchoolProfilePage";
import { SettingsScreen } from "../features/settings/SettingsScreen";
import { StagesSectionsPage } from "../features/stages-sections/StagesSectionsPage";
import { SubjectsPage } from "../features/subjects/SubjectsPage";
import { TeachersPage } from "../features/teachers/TeachersPage";
import { ScheduleStructurePage } from "../features/timetable-structure/ScheduleStructurePage";
import type { Bootstrap } from "../lib/bootstrapQuery";
import { useFormFeedback } from "../lib/useFormFeedback";
import { useUiStore } from "../state/session";
import { MobileMenu } from "./MobileMenu";
import { legacyRedirects, navGroups } from "./navigation";
import { NotFoundPage } from "./NotFoundPage";
import { Sidebar } from "./Sidebar";
import { TopBar } from "./TopBar";
import { useInactivityLock } from "./useInactivityLock";

/** Authenticated frame: top bar, right-hand sidebar (in-flow menu under 768px) and the routed page. */
export function AppShell({ bootstrap }: { bootstrap: Bootstrap }) {
  const feedback = useFormFeedback();
  const collapsed = useUiStore((state) => state.sidebarCollapsed);
  const toggleSidebar = useUiStore((state) => state.toggleSidebar);
  const [menuOpen, setMenuOpen] = useState(false);
  const menuButton = useRef<HTMLButtonElement>(null);
  const closeMenu = useCallback(() => setMenuOpen(false), []);
  const menuButtonElement = useCallback(() => menuButton.current, []);
  useInactivityLock(bootstrap.inactivityTimeoutMinutes);

  return (
    <div className={`app-shell${collapsed ? " is-sidebar-collapsed" : ""}`}>
      <TopBar username={bootstrap.username} menuOpen={menuOpen} menuButtonRef={menuButton} onToggleMenu={() => setMenuOpen((open) => !open)} onError={feedback.showError} />
      <MobileMenu open={menuOpen} onClose={closeMenu} returnFocusTo={menuButtonElement} />
      <div className="app-body">
        <Sidebar collapsed={collapsed} onToggle={toggleSidebar} />
        <main className="app-main">
          <Alert tone="error" message={feedback.error} />
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            {navGroups.map((group) => <Route key={group.root} path={group.root} element={<Navigate to={group.tabs[0].to} replace />} />)}
            <Route path="/school/profile" element={<SchoolProfilePage />} />
            <Route path="/school/year" element={<AcademicYearsPage />} />
            <Route path="/school/timing" element={<ScheduleStructurePage />} />
            <Route path="/school/calendar" element={<CalendarPage />} />
            <Route path="/classes/stages" element={<StagesSectionsPage />} />
            <Route path="/classes/subjects" element={<SubjectsPage />} />
            <Route path="/teachers" element={<TeachersPage />} />
            <Route path="/settings" element={<SettingsScreen bootstrap={bootstrap} />} />
            {Object.entries(legacyRedirects).map(([from, to]) => <Route key={from} path={from} element={<Navigate to={to} replace />} />)}
            <Route path="*" element={<NotFoundPage />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}
