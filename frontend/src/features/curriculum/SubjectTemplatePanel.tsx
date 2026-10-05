import { BookPlus, Check, CircleCheck, Eye, Sparkles, Undo2, X } from "lucide-react";
import { useRef, useState } from "react";
import { InlineAddForm } from "../../components/InlineAddForm";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { IconButton } from "../../components/ui/icon-button";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useArchiveSubject, useSaveSubject, useSubjects, type Subject } from "../subjects/subjectsApi";
import { useSubjectTemplate, useSuggestedSubjects } from "./curriculumApi";
import { PlanList } from "./PlanList";

const text = messages.school.templates;
const sameName = (name: string) => name.trim().replace(/\s+/g, " ");

/**
 * The school's subjects as one chip list (fix B6): added subjects are checked chips marked «مضافة» and can be
 * removed (archived) with an undo; suggestions for the school's stages (names only, spec 2.5 §4.1) are chosen and
 * added with a preview; a new name can be typed. Every change also refreshes the curriculum table.
 */
export function SubjectTemplatePanel({ yearId, format, open = false }: { yearId: number; format: (value: number) => string; open?: boolean }) {
  const feedback = useFormFeedback();
  const { count } = useFormatter();
  const subjects = useSubjects({ search: "", page: 1, pageSize: 100, includeArchived: false });
  const suggested = useSuggestedSubjects(yearId);
  const template = useSubjectTemplate();
  const save = useSaveSubject();
  const archive = useArchiveSubject();
  const formRef = useRef<HTMLFormElement>(null);
  const [skipped, setSkipped] = useState<ReadonlySet<string>>(new Set());
  const [removed, setRemoved] = useState<Subject | null>(null);
  const existing = subjects.data?.items ?? [];
  const existingNames = new Set(existing.map((subject) => sameName(subject.name)));
  const suggestions = (suggested.data ?? []).filter((name) => !existingNames.has(sameName(name)));
  const names = suggestions.filter((name) => !skipped.has(name));
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

  function remove(subject: Subject) {
    feedback.reset();
    archive.mutate({ subject, archived: true }, { onSuccess: (archived) => setRemoved(archived), onError: feedback.showError });
  }

  function undo(subject: Subject) {
    archive.mutate({ subject, archived: false }, {
      onSuccess: (restored) => { setRemoved(null); feedback.showSuccess(text.subjectRestored(restored.name)); },
      onError: feedback.showError,
    });
  }

  return (
    <details className="advanced-options tool-panel" open={open || undefined}>
      <summary>
        <BookPlus aria-hidden="true" size={20} />
        <span>{text.subjectsTitle}</span>
      </summary>
      <div className="form-stack">
        <p className="card-note">{text.subjectsDescription}</p>
        <fieldset className="choice-group subject-chips">
          <legend>{text.schoolSubjects}</legend>
          {subjects.isSuccess && existing.length === 0 && <p className="card-note">{text.noSchoolSubjects}</p>}
          <ul className="chip-list">
            {existing.map((subject) => (
              <li key={`school-subject-${subject.id}`} className="subject-chip is-added">
                <CircleCheck aria-hidden="true" size={16} />
                <span className="subject-chip-name">{subject.name}</span>
                <Badge tone="success">{text.addedBadge}</Badge>
                <IconButton aria-label={text.removeSubject(subject.name)} title={text.removeSubject(subject.name)} icon={<X size={16} />}
                  disabled={archive.isPending} onClick={() => remove(subject)} />
              </li>
            ))}
          </ul>
        </fieldset>
        {removed && (
          <Alert tone="info" message={text.subjectRemoved(removed.name)}>
            <span>
              <Button size="sm" variant="secondary" icon={<Undo2 aria-hidden="true" size={16} />} loading={archive.isPending} onClick={() => undo(removed)}>{text.undo}</Button>
            </span>
          </Alert>
        )}
        {suggestions.length > 0 && (
          <fieldset className="choice-group">
            <legend className="suggestions-legend">
              <span>{text.suggestions}</span>
              <Badge tone="primary" icon={<Sparkles aria-hidden="true" size={16} />}>{text.suggested}</Badge>
            </legend>
            {suggestions.map((name) => (
              <Checkbox key={`suggested-${name}`} checked={!skipped.has(name)} onChange={() => toggle(name)}>{name}</Checkbox>
            ))}
          </fieldset>
        )}
        {suggested.isSuccess && suggested.data.length === 0 && <Alert tone="info" message={text.noSuggestions} />}
        <Alert tone="success" message={feedback.success} />
        <Alert tone="error" message={feedback.error} />
        {suggestions.length > 0 && (
          <div className="form-actions">
            <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} disabled={names.length === 0} loading={template.preview.isPending}
              onClick={() => { feedback.reset(); template.preview.mutate({ names }, { onError: feedback.showError }); }}>{text.preview}</Button>
            {plan && plan.changes > 0 && (
              <Button icon={<Check aria-hidden="true" size={20} />} loading={template.apply.isPending}
                onClick={() => template.apply.mutate({ names }, {
                  onSuccess: (result) => { template.preview.reset(); feedback.showSuccess(text.subjectsApplied(count(result.changes, "subject", "oblique"))); },
                  onError: feedback.showError,
                })}>{text.apply}</Button>
            )}
          </div>
        )}
        {plan && (
          <PlanList id="subject-template-plan" changes={plan.changes} format={format}
            items={plan.lines.map((line) => ({ key: `subject-${line.name}`, label: line.name, action: line.action }))} />
        )}
        <InlineAddForm label={messages.school.wizard.curriculum.quickAdd} buttonLabel={messages.school.wizard.curriculum.quickAdd} pending={save.isPending} formRef={formRef}
          onSubmit={(form, element) => {
            feedback.reset();
            const name = String(form.get("newSchoolSubject") ?? "").trim();
            if (!name) { feedback.showFieldErrors({ Name: messages.errors.REQUIRED }); return; }
            save.mutate({ id: null, input: { name, colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false, requiresDoublePeriod: false, blockedPeriods: [], notes: null, version: 0 } }, {
              onSuccess: () => { element.reset(); feedback.showSuccess(messages.school.subjects.added(name)); },
              onError: feedback.showError,
            });
          }}>
          <TextField id="newSchoolSubject" label={messages.school.subjects.newName} maxLength={100} field="Name" errors={feedback.fieldErrors} />
        </InlineAddForm>
      </div>
    </details>
  );
}
