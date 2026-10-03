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
      <h2 id={id}>{title}</h2>
      <div className="guide-section-body">{children}</div>
    </Card>
  );
}
