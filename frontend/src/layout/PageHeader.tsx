import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { useLocation } from "react-router-dom";
import { Breadcrumbs, type Crumb } from "../components/ui/breadcrumbs";
import { TabNav } from "../components/ui/tab-nav";
import { messages } from "../i18n/messages";
import { groupFor } from "./navigation";

type PageHeaderProps = {
  title: string;
  /** Required: every page has a meaningful lucide icon next to its title. */
  icon: LucideIcon;
  description?: string;
  actions?: ReactNode;
};

/**
 * Breadcrumbs (dashboard → group → page), the page title and, for grouped screens, the group's tabs.
 * The group is derived from the URL, so pages never repeat the navigation structure.
 */
export function PageHeader({ title, icon: Icon, description, actions }: PageHeaderProps) {
  const { pathname } = useLocation();
  const group = groupFor(pathname);
  const isDashboard = pathname === "/";
  const crumbs: Crumb[] = isDashboard
    ? [{ label: title }]
    : [{ label: messages.school.nav.dashboard, to: "/" }, ...(group ? [{ label: group.label, to: group.tabs[0].to }] : []), { label: title }];
  return (
    <header className="page-header">
      <Breadcrumbs label={messages.school.nav.breadcrumbs} items={crumbs} />
      <div className="page-header-row">
        <div className="page-header-text">
          <h1 className="page-title"><Icon aria-hidden="true" size={28} /><span>{title}</span></h1>
          {description && <p>{description}</p>}
        </div>
        {actions && <div className="page-header-actions">{actions}</div>}
      </div>
      {group && <TabNav label={group.label} tabs={group.tabs} />}
    </header>
  );
}
