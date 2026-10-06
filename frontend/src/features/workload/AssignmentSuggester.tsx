import { Check, WandSparkles } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useAssignmentSuggestions, type AssignmentSuggestionPlan } from "./workloadApi";

const text = messages.school.workload.suggester;

function SuggestionPlanView({ plan }: { plan: AssignmentSuggestionPlan }) {
  const format = useFormatter();
  if (plan.assignments.length === 0 && plan.unassigned.length === 0) return <Alert tone="info" message={text.noSuggestions} />;
  return (
    <div className="form-stack">
      {plan.assignments.length > 0 && (
        <section aria-labelledby="suggestion-assignments-title">
          <h3 id="suggestion-assignments-title">{text.assignments}</h3>
          <ul className="reference-list plan-lines">
            {plan.assignments.map((item) => (
              <li key={`${item.sectionId}-${item.entryId}`}>
                {text.assignmentLine(item.stageName, item.sectionLabel, item.label ? `${item.subjectName} - ${item.label}` : item.subjectName,
                  format.number(item.weeklyLessons), item.teacherName)}
              </li>
            ))}
          </ul>
        </section>
      )}
      {plan.loads.length > 0 && (
        <section aria-labelledby="suggestion-loads-title">
          <h3 id="suggestion-loads-title">{text.loads}</h3>
          <ul className="reference-list">
            {plan.loads.map((load) => (
              <li key={load.teacherId}>{text.loadLine(load.teacherName, format.number(load.before), format.number(load.after), format.number(load.limit))}</li>
            ))}
          </ul>
        </section>
      )}
      {plan.unassigned.length > 0 && (
        <section aria-labelledby="suggestion-unassigned-title">
          <h3 id="suggestion-unassigned-title">{text.unassigned}</h3>
          <ul className="reference-list">
            {plan.unassigned.map((item) => (
              <li key={`${item.sectionId}-${item.entryId}`}>
                <Badge tone="warning">{text.reasons[item.reason as keyof typeof text.reasons] ?? text.reasons.fallback}</Badge>
                <p>{text.unassignedLine(item.stageName, item.sectionLabel, item.label ? `${item.subjectName} - ${item.label}` : item.subjectName,
                  text.reasons[item.reason as keyof typeof text.reasons] ?? text.reasons.fallback)}</p>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

export function AssignmentSuggester({ yearId, embedded = false }: { yearId: number; embedded?: boolean }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const { preview, apply } = useAssignmentSuggestions(yearId);
  const [plan, setPlan] = useState<AssignmentSuggestionPlan | null>(null);
  const [confirming, setConfirming] = useState(false);
  const content = (
    <>
      <div>
        {/* Inside the wizard the step title is the h2, so the suggester is a sub-section. */}
        {embedded ? <h3 id="assignment-suggester-title">{text.title}</h3> : <h2 id="assignment-suggester-title">{text.title}</h2>}
        <p className="card-note">{text.description}</p>
      </div>
      <div className="form-actions">
        <Button variant="secondary" icon={<WandSparkles aria-hidden="true" size={20} />} loading={preview.isPending} onClick={() => {
          feedback.reset();
          preview.mutate(undefined, { onSuccess: setPlan, onError: feedback.showError });
        }}>{text.preview}</Button>
      </div>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {plan && <SuggestionPlanView plan={plan} />}
      {plan && plan.assignments.length > 0 && (
        <div className="form-actions">
          <Button icon={<Check aria-hidden="true" size={20} />} onClick={() => setConfirming(true)}>{text.apply}</Button>
        </div>
      )}
      <ConfirmDialog
        open={confirming && plan !== null}
        title={text.confirmTitle}
        consequence={text.confirmConsequence(format.count(plan?.assignments.length ?? 0, "assignment"))}
        confirmLabel={text.apply}
        confirmIcon={<Check aria-hidden="true" size={20} />}
        loading={apply.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() => apply.mutate(undefined, {
          onSuccess: (result) => {
            setPlan(null);
            feedback.showSuccess(result.assignments.length === 0 ? text.nothingApplied : text.applied(format.count(result.assignments.length, "assignment")));
          },
          onError: feedback.showError,
          onSettled: () => setConfirming(false),
        })}
      />
    </>
  );
  return embedded
    ? <section className="assignment-suggester-embedded" aria-labelledby="assignment-suggester-title">{content}</section>
    : <Card className="page-card" aria-labelledby="assignment-suggester-title">{content}</Card>;
}