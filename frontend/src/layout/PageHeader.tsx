import type { ReactNode } from "react";
import { Breadcrumbs, type Crumb } from "../components/ui/breadcrumbs";
import { messages } from "../i18n/messages";

type PageHeaderProps = {
  title: string;
  description?: string;
  /** Trail before the current page; the dashboard is always the root. */
  trail?: readonly Crumb[];
  actions?: ReactNode;
};

export function PageHeader({ title, description, trail = [], actions }: PageHeaderProps) {
  const isDashboard = trail.length === 0 && title === messages.school.nav.dashboard;
  const crumbs: Crumb[] = isDashboard
    ? [{ label: title }]
    : [{ label: messages.school.nav.dashboard, to: "/" }, ...trail, { label: title }];
  return (
    <header className="page-header">
      <Breadcrumbs label={messages.school.nav.breadcrumbs} items={crumbs} />
      <div className="page-header-row">
        <div className="page-header-text">
          <h1>{title}</h1>
          {description && <p>{description}</p>}
        </div>
        {actions && <div className="page-header-actions">{actions}</div>}
      </div>
    </header>
  );
}
