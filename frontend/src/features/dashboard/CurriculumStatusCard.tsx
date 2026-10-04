import { CircleAlert, CircleCheck, CircleX } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "../../components/ui/badge";
import { Card } from "../../components/ui/card";
import { messages } from "../../i18n/messages";
import type { CurriculumStage, ShiftTotal } from "../curriculum/curriculumApi";

const text = messages.school.wizard.dashboardCurriculum;
const status = messages.school.curriculum.status;

function StatusBadge({ total, format }: { total: ShiftTotal; format: (value: number) => string }) {
  if (total.status === "equal") return <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{status.equal}</Badge>;
  if (total.status === "under") return <Badge tone="warning" icon={<CircleAlert aria-hidden="true" size={16} />}>{status.under(format(total.difference))}</Badge>;
  return <Badge tone="danger" icon={<CircleX aria-hidden="true" size={16} />}>{status.over(format(-total.difference))}</Badge>;
}

/** Curriculum totals per stage and shift on the dashboard (spec 2.5 §6), linking to the curriculum screen. */
export function CurriculumStatusCard({ stages, format }: { stages: CurriculumStage[]; format: (value: number) => string }) {
  return (
    <Card className="dashboard-card" aria-labelledby="curriculum-status-title">
      <h2 id="curriculum-status-title"><Link to="/classes/curriculum">{text.title}</Link></h2>
      {stages.length === 0 ? <p className="card-note">{text.empty}</p> : (
        <ul className="curriculum-status">
          {stages.map((stage) => (
            <li key={`curriculum-status-${stage.id}`} className="curriculum-status-row">
              <span className="curriculum-status-name">{stage.name}</span>
              <span className="curriculum-status-planned">{text.planned(format(stage.plannedLessons))}</span>
              {stage.totals.length === 0
                ? <span className="card-note">{messages.school.curriculum.noSections}</span>
                : stage.totals.map((total) => (
                  <span key={`curriculum-status-${stage.id}-${total.shiftId}`} className="curriculum-status-shift">
                    {stage.totals.length > 1 && <span>{total.shiftName}</span>}
                    <StatusBadge total={total} format={format} />
                  </span>
                ))}
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
