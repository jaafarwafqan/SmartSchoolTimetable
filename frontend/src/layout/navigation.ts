import {
  BookOpen, CalendarCheck, CalendarDays, CalendarRange, Clock, Cpu, DoorOpen, Gauge, LayoutDashboard, Layers, Library, School,
  ScrollText, Settings, SlidersHorizontal, UsersRound, type LucideIcon,
} from "lucide-react";
import { messages } from "../i18n/messages";

export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Matches only the exact path (the dashboard at "/"). */
  end?: boolean;
};

export type NavTab = { to: string; label: string; icon: LucideIcon };
export type NavGroup = { root: string; label: string; tabs: readonly NavTab[] };

const nav = messages.school.nav;

/** Groups whose screens are tabs (spec 2.5 §2.6). The group root redirects to its first tab. */
export const navGroups: readonly NavGroup[] = [
  {
    root: "/school",
    label: nav.school,
    tabs: [
      { to: "/school/profile", label: nav.profile, icon: School },
      { to: "/school/year", label: nav.academicYears, icon: CalendarDays },
      { to: "/school/timing", label: nav.scheduleStructure, icon: Clock },
      { to: "/school/calendar", label: nav.calendar, icon: CalendarRange },
    ],
  },
  {
    root: "/classes",
    label: nav.classes,
    tabs: [
      { to: "/classes/stages", label: nav.stagesSections, icon: Layers },
      { to: "/classes/subjects", label: nav.subjects, icon: Library },
      { to: "/classes/curriculum", label: nav.curriculum, icon: BookOpen },
      { to: "/classes/resources", label: nav.resources, icon: DoorOpen },
    ],
  },
  {
    root: "/teachers",
    label: nav.teachers,
    tabs: [
      { to: "/teachers/list", label: nav.teachers, icon: UsersRound },
      { to: "/teachers/workload", label: nav.workload, icon: Gauge },
    ],
  },
  {
    root: "/timetable",
    label: nav.timetable,
    tabs: [
      { to: "/timetable/generate", label: nav.generate, icon: Cpu },
      { to: "/timetable/view", label: nav.timetables, icon: CalendarCheck },
    ],
  },
  {
    root: "/settings",
    label: nav.settings,
    tabs: [
      { to: "/settings/general", label: nav.settingsGeneral, icon: Settings },
      { to: "/settings/advanced", label: nav.settingsAdvanced, icon: SlidersHorizontal },
      { to: "/settings/history", label: nav.settingsHistory, icon: ScrollText },
    ],
  },
];

/** The sidebar entries in display order. */
export const navItems: readonly NavItem[] = [
  { to: "/", label: nav.dashboard, icon: LayoutDashboard, end: true },
  { to: "/school", label: nav.school, icon: School },
  { to: "/classes", label: nav.classes, icon: Library },
  { to: "/teachers", label: nav.teachers, icon: UsersRound },
  { to: "/timetable", label: nav.timetable, icon: CalendarRange },
  { to: "/settings", label: nav.settings, icon: Settings },
];

/** Phase 2 URLs that keep working (bookmarks, links in docs). */
export const legacyRedirects: Readonly<Record<string, string>> = {
  "/academic-years": "/school/year",
  "/schedule-structure": "/school/timing",
  "/calendar": "/school/calendar",
  "/stages-sections": "/classes/stages",
  "/subjects": "/classes/subjects",
};

export function groupFor(pathname: string): NavGroup | undefined {
  return navGroups.find((group) => pathname === group.root || pathname.startsWith(`${group.root}/`));
}
