import { CalendarRange, Clock3, LayoutDashboard, School, Settings, Layers3, type LucideIcon } from "lucide-react";
import { messages } from "../i18n/messages";

export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Matches only the exact path (the dashboard at "/"). */
  end?: boolean;
};

/** Sidebar entries in display order. Each Phase 2 checkpoint adds its screen here. */
export const navItems: readonly NavItem[] = [
  { to: "/", label: messages.school.nav.dashboard, icon: LayoutDashboard, end: true },
  { to: "/school", label: messages.school.nav.profile, icon: School },
  { to: "/academic-years", label: messages.school.nav.academicYears, icon: CalendarRange },
  { to: "/schedule-structure", label: messages.school.nav.scheduleStructure, icon: Clock3 },
  { to: "/stages-sections", label: messages.school.nav.stagesSections, icon: Layers3 },
  { to: "/settings", label: messages.school.nav.settings, icon: Settings },
];
