import { CircleCheck, CircleMinus, CirclePlus, CircleSlash, Copy, PencilLine } from "lucide-react";
import type { ReactNode } from "react";
import { Badge } from "../../components/ui/badge";
import { messages } from "../../i18n/messages";
import type { PlanAction } from "./curriculumApi";

const text = messages.school.templates;

const looks: Record<PlanAction, { tone: "success" | "neutral" | "warning" | "primary"; icon: ReactNode }> = {
  create: { tone: "success", icon: <CirclePlus aria-hidden="true" size={16} /> },
  update: { tone: "primary", icon: <PencilLine aria-hidden="true" size={16} /> },
  exists: { tone: "neutral", icon: <CircleCheck aria-hidden="true" size={16} /> },
  unchanged: { tone: "neutral", icon: <CircleMinus aria-hidden="true" size={16} /> },
  notApplicable: { tone: "neutral", icon: <CircleSlash aria-hidden="true" size={16} /> },
  ambiguous: { tone: "warning", icon: <Copy aria-hidden="true" size={16} /> },
};

export type PlanItem = { key: string; label: ReactNode; action: PlanAction; extra?: ReactNode };

/** The preview of a template or helper: each line with what applying it would do (icon + text, never colour alone). */
export function PlanList({ id, items, changes, format }: { id: string; items: PlanItem[]; changes: number; format: (value: number) => string }) {
  return (
    <section className="plan-preview" aria-labelledby={`${id}-title`}>
      <h4 id={`${id}-title`}>{text.previewTitle}</h4>
      <p className="plan-summary">{changes === 0 ? text.noChanges : text.changes(format(changes))}</p>
      <ul className="plan-list">
        {items.map((item) => (
          <li key={item.key} className="plan-line">
            <span className="plan-line-label">{item.label}</span>
            {item.extra}
            <Badge tone={looks[item.action].tone} icon={looks[item.action].icon}>{text.actions[item.action]}</Badge>
          </li>
        ))}
      </ul>
    </section>
  );
}
