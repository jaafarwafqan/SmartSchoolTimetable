import { SectionTitle } from "../../components/ui/section-title";
import { Check, WandSparkles, Link2, Gauge, CircleAlert, Sparkles, TriangleAlert } from "lucide-react";
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
          <SectionTitle level={3} icon={Link2} id="suggestion-assignments-title">{text.assignments}</SectionTitle>
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
          <SectionTitle level={3} icon={Gauge} id="suggestion-loads-title">{text.loads}</SectionTitle>
          <ul className="reference-list">
            {plan.loads.map((load) => (
              <li key={load.teacherId}>{text.loadLine(load.teacherName, format.number(load.before), format.number(load.after), format.number(load.limit))}</li>
            ))}
          </ul>
        </section>
      )}
      {plan.unassigned.length > 0 && (
        <section aria-labelledby="suggestion-unassigned-title">
          <SectionTitle level={3} icon={CircleAlert} id="suggestion-unassigned-title">{text.unassigned}</SectionTitle>
          <ul className="reference-list">
            {plan.unassigned.map((item) => (
              <li key={`${item.sectionId}-${item.entryId}`}>
                <Badge tone="warning" icon={<TriangleAlert aria-hidden="true" size={16} />}>{text.reasons[item.reason as keyof typeof text.reasons] ?? text.reasons.fallback}</Badge>
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
        <SectionTitle level={embedded ? 3 : 2} icon={Sparkles} id="assignment-suggester-title">{text.title}</SectionTitle>
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