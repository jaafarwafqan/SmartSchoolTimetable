import { SectionTitle } from "../../components/ui/section-title";
import { Palette } from "lucide-react";
import type { ReactNode } from "react";
import { Card } from "../../components/ui/card";

type GuideSectionProps = {
  id: string;
  title: string;
  children: ReactNode;
};

export function GuideSection({ id, title, children }: GuideSectionProps) {
  return (
    <Card className="guide-section" aria-labelledby={id}>
      <SectionTitle level={2} icon={Palette} id={id}>{title}</SectionTitle>
      <div className="guide-section-body">{children}</div>
    </Card>
  );
}
