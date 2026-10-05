import { LayoutDashboard, Library, School, Settings, UsersRound, type LucideIcon } from "lucide-react";
import { messages } from "../i18n/messages";

export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Matches only the exact path (the dashboard at "/"). */
  end?: boolean;
};

export type NavTab = { to: string; label: string };
export type NavGroup = { root: string; label: string; tabs: readonly NavTab[] };

const nav = messages.school.nav;

/** Groups whose screens are tabs (spec 2.5 §2.6). The group root redirects to its first tab. */
export const navGroups: readonly NavGroup[] = [
  {
    root: "/school",
    label: nav.school,
    tabs: [
      { to: "/school/profile", label: nav.profile },
      { to: "/school/year", label: nav.academicYears },
      { to: "/school/timing", label: nav.scheduleStructure },
      { to: "/school/calendar", label: nav.calendar },
    ],
  },
  {
    root: "/classes",
    label: nav.classes,
    tabs: [
      { to: "/classes/stages", label: nav.stagesSections },
      { to: "/classes/subjects", label: nav.subjects },
      { to: "/classes/curriculum", label: nav.curriculum },
      { to: "/classes/resources", label: nav.resources },
    ],
  },
  {
    root: "/teachers",
    label: nav.teachers,
    tabs: [
      { to: "/teachers/list", label: nav.teachers },
      { to: "/teachers/workload", label: nav.workload },
    ],
  },
  {
    root: "/settings",
    label: nav.settings,
    tabs: [
      { to: "/settings/general", label: nav.settingsGeneral },
      { to: "/settings/scheduling", label: nav.schedulingProfile },
    ],
  },
];

/** The five sidebar entries in display order. */
export const navItems: readonly NavItem[] = [
  { to: "/", label: nav.dashboard, icon: LayoutDashboard, end: true },
  { to: "/school", label: nav.school, icon: School },
  { to: "/classes", label: nav.classes, icon: Library },
  { to: "/teachers", label: nav.teachers, icon: UsersRound },
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
