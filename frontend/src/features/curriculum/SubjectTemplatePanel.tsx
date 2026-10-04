import { BookPlus, Check, Eye, Sparkles } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSubjectTemplate, useSuggestedSubjects } from "./curriculumApi";
import { PlanList } from "./PlanList";

const text = messages.school.templates;

/** Suggested subject NAMES for the school's stages (spec 2.5 §4.1); existing subjects are skipped, never renamed. */
export function SubjectTemplatePanel({ yearId, format, open = false }: { yearId: number; format: (value: number) => string; open?: boolean }) {
  const feedback = useFormFeedback();
  const suggested = useSuggestedSubjects(yearId);
  const template = useSubjectTemplate();
  const [skipped, setSkipped] = useState<ReadonlySet<string>>(new Set());
  const names = (suggested.data ?? []).filter((name) => !skipped.has(name));
  const plan = template.preview.data;

  function toggle(name: string) {
    setSkipped((current) => {
      const next = new Set(current);
      if (!next.delete(name)) next.add(name);
      return next;
    });
    template.preview.reset();
    feedback.reset();
  }

  return (
    <details className="advanced-options tool-panel" open={open || undefined}>
      <summary>
        <BookPlus aria-hidden="true" size={20} />
        <span>{text.subjectsTitle}</span>
        <Badge tone="primary" icon={<Sparkles aria-hidden="true" size={16} />}>{text.suggested}</Badge>
      </summary>
      <div className="form-stack">
        <p className="card-note">{text.subjectsDescription}</p>
        {suggested.isSuccess && suggested.data.length === 0 && <Alert tone="info" message={text.noSuggestions} />}
        {(suggested.data?.length ?? 0) > 0 && (
          <fieldset className="choice-group choice-group-columns">
            <legend>{messages.school.nav.subjects}</legend>
            {(suggested.data ?? []).map((name) => (
              <Checkbox key={`suggested-${name}`} checked={!skipped.has(name)} onChange={() => toggle(name)}>{name}</Checkbox>
            ))}
          </fieldset>
        )}
        <Alert tone="success" message={feedback.success} />
        <Alert tone="error" message={feedback.error} />
        <div className="form-actions">
          <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} disabled={names.length === 0} loading={template.preview.isPending}
            onClick={() => { feedback.reset(); template.preview.mutate({ names }, { onError: feedback.showError }); }}>{text.preview}</Button>
          {plan && plan.changes > 0 && (
            <Button icon={<Check aria-hidden="true" size={20} />} loading={template.apply.isPending}
              onClick={() => template.apply.mutate({ names }, {
                onSuccess: (result) => { template.preview.reset(); feedback.showSuccess(text.subjectsApplied(format(result.changes))); },
                onError: feedback.showError,
              })}>{text.apply}</Button>
          )}
        </div>
        {plan && (
          <PlanList id="subject-template-plan" changes={plan.changes} format={format}
            items={plan.lines.map((line) => ({ key: `subject-${line.name}`, label: line.name, action: line.action }))} />
        )}
      </div>
    </details>
  );
}
