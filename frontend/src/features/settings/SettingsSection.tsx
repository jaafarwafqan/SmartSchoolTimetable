import type { ReactNode } from "react";
import { Card } from "../../components/ui/card";

type SettingsSectionProps = {
  id: string;
  icon: ReactNode;
  title: string;
  description: string;
  children: ReactNode;
};

export function SettingsSection({ id, icon, title, description, children }: SettingsSectionProps) {
  return (
    <Card className="settings-section" aria-labelledby={`${id}-title`}>
      <header className="settings-section-header">
        <span className="icon-badge" aria-hidden="true">{icon}</span>
        <div>
          <h2 id={`${id}-title`}>{title}</h2>
          <p>{description}</p>
        </div>
      </header>
      <div className="settings-section-body">{children}</div>
    </Card>
  );
}
