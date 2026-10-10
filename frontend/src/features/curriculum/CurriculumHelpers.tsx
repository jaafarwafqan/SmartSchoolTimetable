import { SectionTitle } from "../../components/ui/section-title";
import { Check, CopyPlus, Eye, ListChecks, Repeat2 } from "lucide-react";
import { useRef, useState } from "react";
import { InlineAddForm } from "../../components/InlineAddForm";
import { SelectField } from "../../components/SelectField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Input } from "../../components/ui/input";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { parseLessons } from "./CurriculumGrid";
import { useCopyCurriculum, useSetAcross, useSetCell, type CurriculumPlan, type CurriculumTable } from "./curriculumApi";
import { PlanList } from "./PlanList";

const text = messages.school.curriculum;

type HelperProps = { yearId: number; table: CurriculumTable; format: (value: number) => string };

const subjectsOf = (table: CurriculumTable) =>
  [...new Map(table.rows.map((row) => [row.subjectId, row.subjectName])).entries()].map(([id, name]) => ({ value: String(id), label: name }));

function StageChecks({ id, legend, table, chosen, exclude, onToggle }: {
  id: string; legend: string; table: CurriculumTable; chosen: ReadonlySet<number>; exclude?: number; onToggle: (stageId: number) => void;
}) {
  return (
    <fieldset className="choice-group" id={id}>
      <legend>{legend}</legend>
      {table.stages.filter((stage) => stage.id !== exclude).map((stage) => (
        <Checkbox key={`${id}-${stage.id}`} checked={chosen.has(stage.id)} onChange={() => onToggle(stage.id)}>{stage.name}</Checkbox>
      ))}
    </fieldset>
  );
}

function planItems(plan: CurriculumPlan, format: (value: number) => string) {
  return plan.lines.map((line, index) => ({
    key: `${line.stageId}-${line.subjectId}-${line.label ?? ""}-${index}`,
    label: text.planLine(line.stageName, line.label ? `${line.subjectName} - ${line.label}` : line.subjectName, format(line.weeklyLessons)),
    action: line.action,
  }));
}

function toggled(set: ReadonlySet<number>, id: number) {
  const next = new Set(set);
  if (!next.delete(id)) next.add(id);
  return next;
}

/** "إضافة تكرار لهذه المادة": a second (or third) line of a subject in a stage, told apart by a label. */
export function AddRepeatForm({ yearId, table }: Omit<HelperProps, "format">) {
  const feedback = useFormFeedback();
  const formRef = useRef<HTMLFormElement>(null);
  const save = useSetCell(yearId);
  const subjects = subjectsOf(table);
  const stages = table.stages.map((stage) => ({ value: String(stage.id), label: stage.name }));

  return (
    <section className="tool-section" aria-labelledby="repeat-title">
      <SectionTitle level={3} icon={Repeat2} id="repeat-title" className="tool-title">{text.addRepeatTitle}</SectionTitle>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <InlineAddForm label={text.addRepeatTitle} buttonLabel={text.addRepeat} pending={save.isPending} formRef={formRef}
        onSubmit={(form, element) => {
          feedback.reset();
          const lessons = parseLessons(String(form.get("repeatLessons") ?? ""));
          if (!lessons.ok || lessons.value === null) { feedback.showFieldErrors({ WeeklyLessons: messages.errors.VALUE_OUT_OF_RANGE }); return; }
          save.mutate({
            stageId: Number(form.get("repeatStage")), subjectId: Number(form.get("repeatSubject")),
            label: String(form.get("repeatLabel") ?? "").trim() || null, weeklyLessons: lessons.value, entryId: null, version: null,
          }, {
            onSuccess: () => { element.reset(); feedback.showSuccess(text.repeatAdded); },
            onError: feedback.showError,
          });
        }}>
        <SelectField id="repeatSubject" label={text.repeatSubject} options={subjects} />
        <TextField id="repeatLabel" label={text.repeatLabel} hint={text.repeatLabelHint} maxLength={40} field="Label" errors={feedback.fieldErrors} />
        <SelectField id="repeatStage" label={text.repeatStage} options={stages} />
        <TextField id="repeatLessons" label={text.repeatLessons} inputMode="numeric" autoComplete="off" field="WeeklyLessons" errors={feedback.fieldErrors} />
      </InlineAddForm>
    </section>
  );
}

/** Copy one stage's lines to other stages: only missing lines are created; nothing is replaced. */
export function CopyCurriculumTool({ yearId, table, format }: HelperProps) {
  const { count } = useFormatter();
  const feedback = useFormFeedback();
  const copy = useCopyCurriculum(yearId);
  const [from, setFrom] = useState<number | null>(null);
  const [targets, setTargets] = useState<ReadonlySet<number>>(new Set());
  const source = table.stages.find((stage) => stage.id === from)?.id ?? table.stages[0]?.id ?? 0;
  const input = { fromStageId: source, toStageIds: [...targets].filter((id) => id !== source) };
  const reset = () => { copy.preview.reset(); feedback.reset(); };
  const plan = copy.preview.data;

  return (
    <details className="advanced-options tool-panel">
      <summary><CopyPlus aria-hidden="true" size={20} /><span>{text.copyTitle}</span></summary>
      <div className="form-stack">
        <p className="card-note">{text.copyDescription}</p>
        <Field id="copy-from" label={text.copyFrom}>
          <Select id="copy-from" value={String(source)} onChange={(event) => { setFrom(Number(event.target.value)); reset(); }}
            options={table.stages.map((stage) => ({ value: String(stage.id), label: stage.name }))} />
        </Field>
        <StageChecks id="copy-to" legend={text.copyTo} table={table} chosen={targets} exclude={source}
          onToggle={(id) => { setTargets((current) => toggled(current, id)); reset(); }} />
        <Alert tone="success" message={feedback.success} />
        <Alert tone="error" message={feedback.error} />
        <div className="form-actions">
          <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} loading={copy.preview.isPending}
            onClick={() => input.toStageIds.length === 0 ? feedback.setError(text.chooseStages) : copy.preview.mutate(input, { onError: feedback.showError })}>
            {messages.school.templates.preview}
          </Button>
          {plan && plan.changes > 0 && (
            <Button icon={<Check aria-hidden="true" size={20} />} loading={copy.apply.isPending}
              onClick={() => copy.apply.mutate(input, {
                onSuccess: (result) => { copy.preview.reset(); feedback.showSuccess(text.copyApplied(count(result.changes, "line", "oblique"))); },
                onError: feedback.showError,
              })}>{messages.school.templates.apply}</Button>
          )}
        </div>
        {plan && <PlanList id="copy-plan" changes={plan.changes} format={format} items={planItems(plan, format)} />}
      </div>
    </details>
  );
}

/** The same weekly lessons for one subject line across chosen stages (creates or updates; never deletes). */
export function SetAcrossTool({ yearId, table, format }: HelperProps) {
  const { count } = useFormatter();
  const feedback = useFormFeedback();
  const across = useSetAcross(yearId);
  const subjects = subjectsOf(table);
  const [subject, setSubject] = useState<number | null>(null);
  const [label, setLabel] = useState("");
  const [lessons, setLessons] = useState("");
  const [stages, setStages] = useState<ReadonlySet<number>>(new Set());
  const subjectId = subject ?? Number(subjects[0]?.value ?? 0);
  const reset = () => { across.preview.reset(); feedback.reset(); };
  const plan = across.preview.data;

  function build() {
    const parsed = parseLessons(lessons);
    if (!parsed.ok || parsed.value === null) { feedback.showFieldErrors({ WeeklyLessons: messages.errors.VALUE_OUT_OF_RANGE }); return null; }
    if (stages.size === 0) { feedback.setError(text.chooseStages); return null; }
    return { subjectId, label: label.trim() || null, weeklyLessons: parsed.value, stageIds: [...stages] };
  }

  return (
    <details className="advanced-options tool-panel">
      <summary><ListChecks aria-hidden="true" size={20} /><span>{text.acrossTitle}</span></summary>
      <div className="form-stack">
        <p className="card-note">{text.acrossDescription}</p>
        <div className="form-grid">
          <Field id="across-subject" label={text.acrossSubject}>
            <Select id="across-subject" value={String(subjectId)} options={subjects} onChange={(event) => { setSubject(Number(event.target.value)); reset(); }} />
          </Field>
          <Field id="across-label" label={text.repeatLabel} hint={text.repeatLabelHint}>
            <Input id="across-label" value={label} maxLength={40} aria-describedby="across-label-hint" onChange={(event) => { setLabel(event.target.value); reset(); }} />
          </Field>
          <Field id="across-lessons" label={text.acrossLessons} error={feedback.fieldErrors.WeeklyLessons}>
            <Input id="across-lessons" value={lessons} inputMode="numeric" autoComplete="off" aria-invalid={feedback.fieldErrors.WeeklyLessons ? true : undefined}
              aria-describedby={feedback.fieldErrors.WeeklyLessons ? "across-lessons-error" : undefined} onChange={(event) => { setLessons(event.target.value); reset(); }} />
          </Field>
        </div>
        <StageChecks id="across-stages" legend={text.acrossStages} table={table} chosen={stages} onToggle={(id) => { setStages((current) => toggled(current, id)); reset(); }} />
        <Alert tone="success" message={feedback.success} />
        <Alert tone="error" message={feedback.error} />
        <div className="form-actions">
          <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} loading={across.preview.isPending}
            onClick={() => { const input = build(); if (input) across.preview.mutate(input, { onError: feedback.showError }); }}>
            {messages.school.templates.preview}
          </Button>
          {plan && plan.changes > 0 && (
            <Button icon={<Check aria-hidden="true" size={20} />} loading={across.apply.isPending}
              onClick={() => { const input = build(); if (input) across.apply.mutate(input, {
                onSuccess: (result) => { across.preview.reset(); feedback.showSuccess(text.acrossApplied(count(result.changes, "line", "oblique"))); },
                onError: feedback.showError,
              }); }}>{messages.school.templates.apply}</Button>
          )}
        </div>
        {plan && <PlanList id="across-plan" changes={plan.changes} format={format} items={planItems(plan, format)} />}
      </div>
    </details>
  );
}
