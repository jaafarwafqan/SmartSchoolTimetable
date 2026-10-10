import type { LucideIcon } from "lucide-react";
import { NavLink } from "react-router-dom";

type TabNavProps = { label: string; tabs: readonly { to: string; label: string; icon: LucideIcon }[] };

/** Route tabs inside a navigation group; the current tab carries aria-current="page" (NavLink). */
export function TabNav({ label, tabs }: TabNavProps) {
  return (
    <nav className="ui-tabs" aria-label={label}>
      <ul>
        {tabs.map((tab) => (
          <li key={tab.to}>
            <NavLink to={tab.to} className={({ isActive }) => `ui-tab${isActive ? " is-active" : ""}`}>
              <tab.icon aria-hidden="true" size={18} />
              <span>{tab.label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
