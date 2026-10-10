import { Check, Eye, LayoutTemplate, Pencil, School, Sparkles } from "lucide-react";
import { Link } from "react-router-dom";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { Stepper } from "../../components/ui/stepper";
import { userErrorMessage } from "../../api";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useOutOfTypeStages, useStageTemplate, useTemplateCatalog, type LabelStyle, type StageTemplateInput } from "../curriculum/curriculumApi";
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
export function StageTemplatePanel({ yearId, open = false }: { yearId: number; open?: boolean }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const catalog = useTemplateCatalog();
  const profile = useSchoolProfile();
  const shifts = useShifts(yearId);
  const template = useStageTemplate(yearId);
  const [skipped, setSkipped] = useState<ReadonlySet<string>>(new Set());
  const [sections, setSections] = useState(2);
  const [style, setStyle] = useState<LabelStyle>("arabic");
  const profileType = profile.data?.schoolType;
  const schoolType = isTemplateType(profileType) ? profileType : null;
  const shiftList = shifts.data?.items ?? [];
  const shiftId = shiftList[0]?.id ?? null;
  const grades = (catalog.data?.grades ?? []).filter((grade) => schoolType !== null && grade.schoolTypes.includes(schoolType));
  const branches = catalog.data?.branches ?? [];
  const outOfTypeStages = useOutOfTypeStages(yearId, schoolType);
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

  const selectedGrades = grades.filter((grade) =>
    !skipped.has(grade.key) &&
    (grade.branchStem === null || branches.some((branch) => !skipped.has(`${grade.key}-${branch.key}`))));
  const input: StageTemplateInput = {
    schoolType: schoolType ?? "",
    grades: selectedGrades.map((grade) => ({
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
      <details className="advanced-options tool-panel" open={open || undefined}>
        <summary>
          <LayoutTemplate aria-hidden="true" size={20} />
          <span>{text.stagesTitle}</span>
          <Badge tone="primary" icon={<Sparkles aria-hidden="true" size={16} />}>{text.suggested}</Badge>
        </summary>
        <div className="form-stack">
          <p className="card-note">{text.stagesDescription}</p>
          <p className="school-type-readonly">
            <School aria-hidden="true" size={18} />
            <span>{schoolType ? text.schoolTypeIs(messages.school.profile.schoolTypes[schoolType]) : text.schoolTypeMissing}</span>
            <Link className="link-button" to="/school/profile"><Pencil aria-hidden="true" size={16} /><span>{text.changeSchoolType}</span></Link>
          </p>
          {!schoolType && <Alert tone="info" message={text.schoolTypeRequired} />}
          {outOfTypeStages.isError && <Alert tone="error" message={userErrorMessage(outOfTypeStages.error)} />}
          {outOfTypeStages.data && outOfTypeStages.data.length > 0 && (
            <Alert tone="warning" message={text.outOfTypeStages}>
              <ul className="plan-list">
                {outOfTypeStages.data.map((stage) => <li key={stage.id}>{stage.name}</li>)}
              </ul>
            </Alert>
          )}
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
          <NewSectionOptions idPrefix="template" style={style} onStyle={edit(setStyle)} />
          {nothingSelected && schoolType && <Alert tone="info" message={text.nothingSelected} />}
          <Alert tone="success" message={feedback.success} />
          <Alert tone="error" message={feedback.error} />
          <div className="form-actions">
            <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} disabled={!schoolType || nothingSelected} loading={template.preview.isPending}
              onClick={() => { feedback.reset(); template.preview.mutate(input, { onError: feedback.showError }); }}>{text.preview}</Button>
            {plan && plan.changes > 0 && schoolType && (
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
                extra: line.sectionsToAdd > 0 ? <span className="plan-line-extra">{text.addSections(format.count(line.sectionsToAdd, "section", "oblique"))}</span> : undefined,
              }))} />
          )}
        </div>
      </details>
    </Card>
  );
}
