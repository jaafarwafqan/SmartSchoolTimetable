import { TriangleAlert, UsersRound } from "lucide-react";
import { LoadBar, LoadStatusBadge } from "../../components/LoadBar";
import { Badge } from "../../components/ui/badge";
import { EmptyState } from "../../components/ui/empty-state";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import type { TeacherLoad } from "./workloadApi";

const text = messages.school.workload;

/** «حسب المعلم» (Phase 3 §4): each teacher's load bar (assigned / max per week / available) and status. */
export function TeacherLoads({ loads }: { loads: readonly TeacherLoad[] }) {
  const format = useFormatter();
  if (loads.length === 0) return <EmptyState icon={<UsersRound aria-hidden="true" size={24} />} message={text.noTeachers} />;
  return (
    <ul className="teacher-loads" aria-label={text.views.byTeacher}>
      {loads.map((load) => (
        <li key={load.teacherId} className="teacher-load">
          <div className="teacher-load-head">
            <strong>{load.fullName}</strong>
            <LoadStatusBadge status={load.status} />
            {load.released && <Badge tone="warning" icon={<UsersRound aria-hidden="true" size={16} />}>{text.released}</Badge>}
          </div>
          <LoadBar label={text.loadBar(load.fullName, format.number(load.assignedLessons), format.number(load.limit))}
            assigned={load.assignedLessons} limit={load.limit} status={load.status} />
          <span className="card-note">
            {text.loadNumbers(format.number(load.assignedLessons), load.maxPerWeek === null ? text.noLimit : format.number(load.maxPerWeek), format.number(load.available))}
          </span>
          <details className="advanced-options">
            <summary>{format.count(load.assignments.length, "assignment")}</summary>
            {load.assignments.length === 0 ? <p className="card-note">{text.noAssignments}</p> : (
              <ul className="reference-list">
                {load.assignments.map((item) => (
                  <li key={item.assignmentId}>
                    {text.assignmentLine(item.stageName, item.sectionLabel, item.label ? `${item.subjectName} - ${item.label}` : item.subjectName, format.number(item.weeklyLessons))}
                    {item.outsideSpecialization && <> <Badge tone="warning" icon={<TriangleAlert aria-hidden="true" size={16} />}>{text.outside}</Badge></>}
                  </li>
                ))}
              </ul>
            )}
          </details>
        </li>
      ))}
    </ul>
  );
}
