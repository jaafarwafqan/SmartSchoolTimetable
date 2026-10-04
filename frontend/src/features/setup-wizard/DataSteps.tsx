import { CircleAlert, CircleCheck, Flag } from "lucide-react";
import { useRef, useState } from "react";
import { InlineAddForm } from "../../components/InlineAddForm";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { CurriculumEditor } from "../curriculum/CurriculumPage";
import { SubjectTemplatePanel } from "../curriculum/SubjectTemplatePanel";
import { StageCardsPanel } from "../stages-sections/StageCardsPanel";
import { StageTemplatePanel } from "../stages-sections/StageTemplatePanel";
import { useSaveSubject } from "../subjects/subjectsApi";
import { BulkAddPanel } from "../teachers/BulkAddPanel";
import { WizardFooter } from "./WizardFrame";
import { useRecordStep, useSetupReview, type SetupProgress, type SetupWarning } from "./wizardApi";

const text = messages.school.wizard;

type DataStepProps = { progress: SetupProgress; onBack: () => void; onDone: () => void };

/** Steps 4–6 save their data through the screens' own endpoints; "next" records the step in the progress. */
function useStepRecorder(progress: SetupProgress, step: number, onDone: () => void) {
  const feedback = useFormFeedback();
  const record = useRecordStep();
  return {
    feedback,
    pending: record.isPending,
    next: (skipped = false) => { feedback.reset(); record.mutate({ progress, step, skipped }, { onSuccess: onDone, onError: feedback.showError }); },
  };
}

function NoYear() {
  return <Alert tone="warning" message={messages.school.stageCards.noShift} />;
}

/** Step 4: stages from the template (per-grade shift in dual mode) and the section stepper per stage. */
export function StagesStep({ progress, onBack, onDone }: DataStepProps) {
  const yearId = useSchoolContext().data?.currentYear?.id ?? null;
  const step = useStepRecorder(progress, 4, onDone);
  return (
    <div className="form-stack">
      <p className="card-note">{text.stages.note}</p>
      {yearId === null ? <NoYear /> : (
        <>
          <StageTemplatePanel yearId={yearId} open />
          <StageCardsPanel yearId={yearId} />
        </>
      )}
      <WizardFooter step={4} pending={step.pending} error={step.feedback.error} onBack={onBack} onNext={() => step.next()} />
    </div>
  );
}

/** Step 5: suggested subjects plus quick add, then the same curriculum table as the screen. */
export function CurriculumStep({ progress, onBack, onDone }: DataStepProps) {
  const format = useFormatter();
  const yearId = useSchoolContext().data?.currentYear?.id ?? null;
  const step = useStepRecorder(progress, 5, onDone);
  const add = useFormFeedback();
  const saveSubject = useSaveSubject();
  const formRef = useRef<HTMLFormElement>(null);
  return (
    <div className="form-stack">
      <p className="card-note">{text.curriculum.note}</p>
      {yearId === null ? <NoYear /> : (
        <>
          <Card className="page-card">
            <SubjectTemplatePanel yearId={yearId} format={format.number} open />
            <Alert tone="success" message={add.success} />
            <Alert tone="error" message={add.error} />
            <InlineAddForm label={text.curriculum.quickAdd} buttonLabel={text.curriculum.quickAdd} pending={saveSubject.isPending} formRef={formRef}
              onSubmit={(form, element) => {
                add.reset();
                const name = String(form.get("wizardSubjectName") ?? "").trim();
                if (!name) { add.showFieldErrors({ Name: messages.errors.REQUIRED }); return; }
                saveSubject.mutate({ id: null, input: { name, colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false, requiresDoublePeriod: false, blockedPeriods: [], notes: null, version: 0 } }, {
                  onSuccess: () => { element.reset(); add.showSuccess(messages.school.subjects.added(name)); },
                  onError: add.showError,
                });
              }}>
              <TextField id="wizardSubjectName" label={messages.school.subjects.newName} maxLength={100} field="Name" errors={add.fieldErrors} />
            </InlineAddForm>
          </Card>
          <CurriculumEditor yearId={yearId} />
        </>
      )}
      <WizardFooter step={5} pending={step.pending} error={step.feedback.error} onBack={onBack} onNext={() => step.next()} />
    </div>
  );
}

/** Step 6 (optional): paste teacher names with a preview; may be skipped. */
export function TeachersStep({ progress, onBack, onDone }: DataStepProps) {
  const format = useFormatter();
  const step = useStepRecorder(progress, 6, onDone);
  const [saved, setSaved] = useState<string | null>(null);
  return (
    <div className="form-stack">
      <p className="card-note">{text.teachers.note}</p>
      <Alert tone="success" message={saved} />
      <BulkAddPanel closable={false} onClose={() => undefined} onSaved={(count) => setSaved(text.teachers.saved(format.number(count)))} />
      <WizardFooter step={6} pending={step.pending} error={step.feedback.error} onBack={onBack} onNext={() => step.next()} onSkip={() => step.next(true)} />
    </div>
  );
}

function warningText(warning: SetupWarning, format: (value: number) => string) {
  const texts = text.review.warningTexts;
  if (warning.code === "under" || warning.code === "over") return texts[warning.code](warning.stageName, warning.shiftName ?? "", format(warning.value));
  return texts[warning.code](warning.stageName);
}

/** Step 7: real counts and what still needs attention; finishing never blocks on warnings. */
export function ReviewStep({ progress, onBack, onFinished }: { progress: SetupProgress; onBack: () => void; onFinished: () => void }) {
  const format = useFormatter();
  const review = useSetupReview(true);
  const feedback = useFormFeedback();
  const record = useRecordStep();
  const data = review.data;
  const counts: [string, string][] = data ? [
    [text.review.school, data.schoolName || text.review.none],
    [text.review.year, data.yearLabel ?? text.review.none],
    [text.review.shifts, format.number(data.shifts)],
    [text.review.stages, format.number(data.stages)],
    [text.review.sections, format.number(data.sections)],
    [text.review.subjects, format.number(data.subjects)],
    [text.review.curriculumLines, format.number(data.curriculumLines)],
    [text.review.teachers, format.number(data.teachers)],
  ] : [];

  return (
    <div className="form-stack">
      {review.isPending && <Spinner label={messages.app.loadingContent} />}
      {review.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {data && (
        <>
          <section aria-labelledby="review-counts">
            <h3 id="review-counts">{text.review.counts}</h3>
            <dl className="count-grid">
              {counts.map(([label, value]) => (
                <div key={`review-${label}`} className="count-item">
                  <dt>{label}</dt>
                  <dd className="count-value"><bdi>{value}</bdi></dd>
                </div>
              ))}
            </dl>
          </section>
          <section aria-labelledby="review-warnings">
            <h3 id="review-warnings">{text.review.warnings}</h3>
            {data.warnings.length === 0
              ? <p className="review-ok"><CircleCheck aria-hidden="true" size={18} /><span>{text.review.noWarnings}</span></p>
              : (
                <ul className="review-warnings">
                  {data.warnings.map((warning, index) => (
                    <li key={`warning-${warning.code}-${warning.stageName}-${warning.shiftName ?? ""}-${index}`}>
                      <CircleAlert aria-hidden="true" size={18} />
                      <span>{warningText(warning, format.number)}</span>
                    </li>
                  ))}
                </ul>
              )}
            <p className="card-note">{text.review.finishedNote}</p>
          </section>
        </>
      )}
      <WizardFooter step={7} pending={record.isPending} error={feedback.error} nextLabel={text.finish} nextIcon={<Flag aria-hidden="true" size={20} />} onBack={onBack}
        onNext={() => { feedback.reset(); record.mutate({ progress, step: 7, finish: true }, { onSuccess: onFinished, onError: feedback.showError }); }} />
    </div>
  );
}
