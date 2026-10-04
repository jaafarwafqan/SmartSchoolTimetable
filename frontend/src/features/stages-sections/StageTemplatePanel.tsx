import { Check, Eye, LayoutTemplate, Sparkles } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { Stepper } from "../../components/ui/stepper";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useStageTemplate, useTemplateCatalog, type LabelStyle, type StageTemplateInput } from "../curriculum/curriculumApi";
import { PlanList } from "../curriculum/PlanList";
import { useSchoolProfile } from "../school-profile/profileApi";
import { useShifts } from "../timetable-structure/scheduleApi";
import { NewSectionOptions } from "./StageCardsPanel";

const text = messages.school.templates;
const schoolTypes = ["primary", "intermediate", "preparatory", "secondary"] as const;
type TemplateSchoolType = (typeof schoolTypes)[number];
const isTemplateType = (value: string | undefined): value is TemplateSchoolType => schoolTypes.some((type) => type === value);

/**
 * Stage template (spec 2.5 §4.1): pick grades (and branches for the preparatory grades) and a section count,
 * preview what would be created, then apply. Applying twice changes nothing (ADR 0022).
 */
export function StageTemplatePanel({ yearId }: { yearId: number }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const catalog = useTemplateCatalog();
  const profile = useSchoolProfile();
  const shifts = useShifts(yearId);
  const template = useStageTemplate(yearId);
  const [typeChoice, setTypeChoice] = useState<TemplateSchoolType | null>(null);
  const [skipped, setSkipped] = useState<ReadonlySet<string>>(new Set());
  const [sections, setSections] = useState(2);
  const [shiftChoice, setShiftChoice] = useState<number | null>(null);
  const [style, setStyle] = useState<LabelStyle>("arabic");
  const profileType = profile.data?.schoolType;
  const schoolType = typeChoice ?? (isTemplateType(profileType) ? profileType : "intermediate");
  const shiftList = shifts.data?.items ?? [];
  const shiftId = shiftList.find((shift) => shift.id === shiftChoice)?.id ?? shiftList[0]?.id ?? null;
  const grades = (catalog.data?.grades ?? []).filter((grade) => grade.schoolTypes.includes(schoolType));
  const branches = catalog.data?.branches ?? [];
  const plan = template.preview.data;

  /** Any change invalidates the preview, so "apply" always applies what was last shown. */
  function edit<T>(setter: (value: T) => void) {
    return (value: T) => { setter(value); template.preview.reset(); feedback.reset(); };
  }
  const toggle = edit((key: string) => setSkipped((current) => {
    const next = new Set(current);
    if (!next.delete(key)) next.add(key);
    return next;
  }));

  const input: StageTemplateInput = {
    schoolType,
    grades: grades.filter((grade) => !skipped.has(grade.key)).map((grade) => ({
      gradeKey: grade.key,
      branches: grade.branchStem ? branches.map((branch) => branch.key).filter((key) => !skipped.has(`${grade.key}-${key}`)) : [],
      sections,
      shiftId,
      labelStyle: style,
    })),
  };
  const nothingSelected = input.grades.length === 0;

  return (
    <Card className="page-card">
      <details className="advanced-options tool-panel">
        <summary>
          <LayoutTemplate aria-hidden="true" size={20} />
          <span>{text.stagesTitle}</span>
          <Badge tone="primary" icon={<Sparkles aria-hidden="true" size={16} />}>{text.suggested}</Badge>
        </summary>
        <div className="form-stack">
          <p className="card-note">{text.stagesDescription}</p>
          <Field id="template-school-type" label={text.schoolType}>
            <Select id="template-school-type" value={schoolType}
              onChange={(event) => edit(setTypeChoice)(event.target.value as TemplateSchoolType)}
              options={schoolTypes.map((value) => ({ value, label: messages.school.profile.schoolTypes[value] }))} />
          </Field>
          <fieldset className="choice-group">
            <legend>{text.grades}</legend>
            {grades.map((grade) => (
              <div key={`grade-${grade.key}`} className="choice-group-item">
                <Checkbox checked={!skipped.has(grade.key)} onChange={() => toggle(grade.key)}>{grade.name}</Checkbox>
                {grade.branchStem && !skipped.has(grade.key) && (
                  <fieldset className="choice-group choice-group-nested">
                    <legend>{text.branches(grade.name)}</legend>
                    {branches.map((branch) => (
                      <Checkbox key={`branch-${grade.key}-${branch.key}`} checked={!skipped.has(`${grade.key}-${branch.key}`)}
                        onChange={() => toggle(`${grade.key}-${branch.key}`)}>{`${grade.branchStem} ${branch.name}`}</Checkbox>
                    ))}
                  </fieldset>
                )}
              </div>
            ))}
          </fieldset>
          <div className="ui-field">
            <span className="stepper-caption" aria-hidden="true">{text.sectionsPerStage}</span>
            <Stepper id="template-sections" label={text.sectionsPerStage} value={sections} min={0} max={30} format={format.number}
              decreaseLabel={text.sectionsDecrease} increaseLabel={text.sectionsIncrease} onChange={edit(setSections)} />
          </div>
          <NewSectionOptions idPrefix="template" shifts={shiftList} shiftId={shiftId} style={style} onShift={edit(setShiftChoice)} onStyle={edit(setStyle)} />
          {nothingSelected && <Alert tone="info" message={text.nothingSelected} />}
          <Alert tone="success" message={feedback.success} />
          <Alert tone="error" message={feedback.error} />
          <div className="form-actions">
            <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} disabled={nothingSelected} loading={template.preview.isPending}
              onClick={() => { feedback.reset(); template.preview.mutate(input, { onError: feedback.showError }); }}>{text.preview}</Button>
            {plan && plan.changes > 0 && (
              <Button icon={<Check aria-hidden="true" size={20} />} loading={template.apply.isPending}
                onClick={() => template.apply.mutate(input, {
                  onSuccess: (result) => { template.preview.reset(); feedback.showSuccess(text.applied(format.number(result.changes))); },
                  onError: feedback.showError,
                })}>{text.apply}</Button>
            )}
          </div>
          {plan && (
            <PlanList id="stage-template-plan" changes={plan.changes} format={format.number}
              items={plan.lines.map((line) => ({
                key: `${line.key}-${line.action}`,
                label: line.name,
                action: line.action,
                extra: line.sectionsToAdd > 0 ? <span className="plan-line-extra">{text.addSections(format.number(line.sectionsToAdd))}</span> : undefined,
              }))} />
          )}
        </div>
      </details>
    </Card>
  );
}
