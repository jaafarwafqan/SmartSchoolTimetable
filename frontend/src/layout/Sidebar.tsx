import { PanelRightClose, PanelRightOpen } from "lucide-react";
import { NavLink } from "react-router-dom";
import { Button } from "../components/ui/button";
import { messages } from "../i18n/messages";
import { navItems } from "./navigation";

type SidebarProps = {
  collapsed: boolean;
  onToggle?: () => void;
  onNavigate?: () => void;
};

/**
 * Right-hand navigation (RTL start side). Collapsed mode keeps only icons; each link keeps its Arabic
 * label as the accessible name and as a tooltip (DESIGN_SYSTEM.md 6.6).
 */
export function Sidebar({ collapsed, onToggle, onNavigate }: SidebarProps) {
  return (
    <nav className={`app-sidebar${collapsed ? " is-collapsed" : ""}`} aria-label={messages.school.nav.sidebarLabel}>
      <ul>
        {navItems.map(({ to, label, icon: Icon, end }) => (
          <li key={to}>
            <NavLink
              to={to}
              end={end}
              className={({ isActive }) => `sidebar-link${isActive ? " is-active" : ""}`}
              title={collapsed ? label : undefined}
              aria-label={collapsed ? label : undefined}
              onClick={onNavigate}
            >
              <Icon aria-hidden="true" size={20} strokeWidth={2} />
              <span className="sidebar-label">{label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
      {onToggle && (
        <div className="sidebar-footer">
          <Button
            variant="ghost"
            size="sm"
            className="sidebar-toggle"
            icon={collapsed ? <PanelRightOpen aria-hidden="true" size={20} /> : <PanelRightClose aria-hidden="true" size={20} />}
            aria-expanded={!collapsed}
            title={collapsed ? messages.school.nav.expand : messages.school.nav.collapse}
            onClick={onToggle}
          >
            {collapsed ? messages.school.nav.expand : messages.school.nav.collapse}
          </Button>
        </div>
      )}
    </nav>
  );
}
