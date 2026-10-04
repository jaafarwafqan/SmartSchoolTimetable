import { CalendarRange, GraduationCap, Menu as MenuIcon } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "../components/ui/badge";
import { Button } from "../components/ui/button";
import { messages } from "../i18n/messages";
import { useSchoolContext } from "../lib/schoolContext";
import { UserMenu } from "./UserMenu";

type TopBarProps = {
  username: string | null;
  onOpenDrawer: () => void;
  onError: (reason: unknown) => void;
};

/** School name, current academic year and term, and the user menu (DESIGN_SYSTEM.md 6.6). */
export function TopBar({ username, onOpenDrawer, onError }: TopBarProps) {
  const { data: context } = useSchoolContext();
  const year = context?.currentYear;
  const term = context?.currentTerm;
  return (
    <header className="app-topbar">
      <Button
        variant="ghost"
        className="drawer-trigger"
        icon={<MenuIcon aria-hidden="true" size={20} />}
        onClick={onOpenDrawer}
      >
        {messages.school.nav.openMenu}
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
