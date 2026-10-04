import { useState } from "react";
import { Route, Routes } from "react-router-dom";
import { Alert } from "../components/ui/alert";
import { AcademicYearsPage } from "../features/academic-years/AcademicYearsPage";
import { DashboardPage } from "../features/dashboard/DashboardPage";
import { SchoolProfilePage } from "../features/school-profile/SchoolProfilePage";
import { SettingsScreen } from "../features/settings/SettingsScreen";
import { ScheduleStructurePage } from "../features/timetable-structure/ScheduleStructurePage";
import { StagesSectionsPage } from "../features/stages-sections/StagesSectionsPage";
import { SubjectsPage } from "../features/subjects/SubjectsPage";
import { TeachersPage } from "../features/teachers/TeachersPage";
import { CalendarPage } from "../features/calendar/CalendarPage";
import type { Bootstrap } from "../lib/bootstrapQuery";
import { useFormFeedback } from "../lib/useFormFeedback";
import { useUiStore } from "../state/session";
import { MobileDrawer } from "./MobileDrawer";
import { NotFoundPage } from "./NotFoundPage";
import { Sidebar } from "./Sidebar";
import { TopBar } from "./TopBar";
import { useInactivityLock } from "./useInactivityLock";

/** Authenticated application frame: top bar, right-hand sidebar (drawer under 768px) and the routed page. */
export function AppShell({ bootstrap }: { bootstrap: Bootstrap }) {
  const feedback = useFormFeedback();
  const collapsed = useUiStore((state) => state.sidebarCollapsed);
  const toggleSidebar = useUiStore((state) => state.toggleSidebar);
  const [drawerOpen, setDrawerOpen] = useState(false);
  useInactivityLock(bootstrap.inactivityTimeoutMinutes);

  return (
    <div className={`app-shell${collapsed ? " is-sidebar-collapsed" : ""}`}>
      <TopBar username={bootstrap.username} onOpenDrawer={() => setDrawerOpen(true)} onError={feedback.showError} />
      <div className="app-body">
        <Sidebar collapsed={collapsed} onToggle={toggleSidebar} />
        <main className="app-main">
          <Alert tone="error" message={feedback.error} />
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/school" element={<SchoolProfilePage />} />
            <Route path="/academic-years" element={<AcademicYearsPage />} />
            <Route path="/schedule-structure" element={<ScheduleStructurePage />} />
            <Route path="/stages-sections" element={<StagesSectionsPage />} />
            <Route path="/subjects" element={<SubjectsPage />} />
            <Route path="/teachers" element={<TeachersPage />} />
            <Route path="/calendar" element={<CalendarPage />} />
            <Route path="/settings" element={<SettingsScreen bootstrap={bootstrap} />} />
            <Route path="*" element={<NotFoundPage />} />
          </Routes>
        </main>
      </div>
      <MobileDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
    </div>
  );
}
