import { CalendarRange, GraduationCap, Menu as MenuIcon, X } from "lucide-react";
import type { RefObject } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/ui/badge";
import { Button } from "../components/ui/button";
import { messages } from "../i18n/messages";
import { useSchoolContext } from "../lib/schoolContext";
import { mobileMenuId } from "./MobileMenu";
import { UserMenu } from "./UserMenu";

type TopBarProps = {
  username: string | null;
  menuOpen: boolean;
  menuButtonRef: RefObject<HTMLButtonElement | null>;
  onToggleMenu: () => void;
  onError: (reason: unknown) => void;
};

/** School name, current academic year and term, and the user menu (DESIGN_SYSTEM.md 6.6). */
export function TopBar({ username, menuOpen, menuButtonRef, onToggleMenu, onError }: TopBarProps) {
  const { data: context } = useSchoolContext();
  const year = context?.currentYear;
  const term = context?.currentTerm;
  return (
    <header className="app-topbar">
      <Button
        ref={menuButtonRef}
        variant="ghost"
        className="mobile-menu-toggle"
        icon={menuOpen ? <X aria-hidden="true" size={20} /> : <MenuIcon aria-hidden="true" size={20} />}
        aria-expanded={menuOpen}
        aria-controls={mobileMenuId}
        onClick={onToggleMenu}
      >
        {menuOpen ? messages.school.nav.closeMenu : messages.school.nav.openMenu}
      </Button>
      <Link className="brand-link" to="/">
        <span className="brand-logo brand-logo-sm" aria-hidden="true">
          <GraduationCap size={20} strokeWidth={2} />
        </span>
        <span>{context?.schoolName || messages.school.shell.noSchoolName}</span>
      </Link>
      <div className="topbar-context">
        <Badge tone={year ? "primary" : "neutral"} icon={<CalendarRange aria-hidden="true" size={16} />}>
          {year ? messages.school.shell.currentYear(year.name) : messages.school.shell.noCurrentYear}
        </Badge>
        <Badge tone={term ? "primary" : "neutral"}>
          {term ? messages.school.shell.currentTerm(term.name) : messages.school.shell.noCurrentTerm}
        </Badge>
      </div>
      <UserMenu username={username} onError={onError} />
    </header>
  );
}
