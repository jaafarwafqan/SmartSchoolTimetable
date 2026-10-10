import { SectionTitle } from "../../components/ui/section-title";
import { ChevronLeft, CircleCheck, CircleDashed, ListChecks } from "lucide-react";
import { Link } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Card } from "../../components/ui/card";
import { messages } from "../../i18n/messages";
import { checklistSteps } from "./dashboardItems";

type SetupChecklistProps = { steps: Array<{ key: string; done: boolean }> };

/** Computed setup checklist; each unfinished step links to the screen that completes it. */
export function SetupChecklist({ steps }: SetupChecklistProps) {
  const known = steps.filter((step) => checklistSteps[step.key]);
  const allDone = known.length > 0 && known.every((step) => step.done);
  return (
    <Card className="dashboard-card" aria-labelledby="checklist-title">
      <SectionTitle level={2} icon={ListChecks} id="checklist-title">{messages.school.dashboard.checklistTitle}</SectionTitle>
      {allDone && <Alert tone="success" message={messages.school.dashboard.checklistDone} />}
      <ol className="checklist">
        {known.map((step) => {
          const item = checklistSteps[step.key];
          return (
            <li key={step.key} className={`checklist-item${step.done ? " is-done" : ""}`}>
              {step.done
                ? <CircleCheck className="checklist-icon is-done" aria-hidden="true" size={20} />
                : <CircleDashed className="checklist-icon" aria-hidden="true" size={20} />}
              <span className="checklist-label">{item.label}</span>
              {step.done
                ? <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{messages.school.dashboard.stepDone}</Badge>
                : (
                  <Link className="link-button" to={item.to}>
                    <span>{messages.school.dashboard.openStep}</span>
                    <ChevronLeft aria-hidden="true" size={18} />
                    <span className="sr-only">{item.label}</span>
                  </Link>
                )}
            </li>
          );
        })}
      </ol>
    </Card>
  );
}
