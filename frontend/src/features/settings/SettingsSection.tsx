import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Card } from "../../components/ui/card";
import { SectionTitle } from "../../components/ui/section-title";

type SettingsSectionProps = {
  id: string;
  icon: LucideIcon;
  title: string;
  description: string;
  children: ReactNode;
};

export function SettingsSection({ id, icon, title, description, children }: SettingsSectionProps) {
  return (
    <Card className="settings-section" aria-labelledby={`${id}-title`}>
      <header className="settings-section-header">
        <div>
          <SectionTitle level={2} icon={icon} id={`${id}-title`}>{title}</SectionTitle>
          <p>{description}</p>
        </div>
      </header>
      <div className="settings-section-body">{children}</div>
    </Card>
  );
}
