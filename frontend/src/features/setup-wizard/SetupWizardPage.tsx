import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { CurriculumStep, ReviewStep, StagesStep, TeachersStep } from "./DataSteps";
import { SchoolStep } from "./SchoolStep";
import { TimingStep } from "./TimingStep";
import { WizardProgress, wizardSteps, type WizardStep } from "./WizardFrame";
import { useSetupProgress } from "./wizardApi";
import { YearStep } from "./YearStep";

const text = messages.school.wizard;
const asStep = (value: number): WizardStep => wizardSteps.find((step) => step === value) ?? 1;

/**
 * Setup wizard (spec 2.5 §5, ADR 0023): seven linear steps, each saved when moving on, resumed at the stored
 * current step. Steps reuse the screens' components and the normal services; nothing is a shortcut.
 */
export function SetupWizardPage() {
  const format = useFormatter();
  const navigate = useNavigate();
  const progress = useSetupProgress();
  const [chosen, setChosen] = useState<WizardStep | null>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const data = progress.data;
  // The stored step is read once (resume). Afterwards the step is local: a step's own save refetches the progress,
  // and following it would unmount the step before its success callback runs.
  const resumeStep = useRef<WizardStep | null>(null);
  if (resumeStep.current === null && data) resumeStep.current = asStep(data.isFinished ? 1 : data.currentStep);
  const step = chosen ?? resumeStep.current ?? 1;

  // Moving between steps puts focus on the step title so keyboard and screen-reader users start at the top.
  useEffect(() => {
    if (chosen !== null) headingRef.current?.focus();
  }, [chosen]);

  const go = (next: number) => setChosen(asStep(next));
  const back = () => go(step - 1);
  const done = () => go(step + 1);

  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} />
      {progress.isPending && <Spinner label={messages.app.loadingContent} />}
      {progress.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {data && (
        <div className="wizard-layout">
          <WizardProgress step={step} progress={data} format={format.number} />
          <Card className="page-card wizard-card" aria-labelledby="wizard-step-title">
            <h2 id="wizard-step-title" ref={headingRef} tabIndex={-1}>{text.steps[step]}</h2>
            {step === 1 && <SchoolStep onBack={back} onDone={done} />}
            {step === 2 && <YearStep onBack={back} onDone={done} />}
            {step === 3 && <TimingStep progress={data} onBack={back} onDone={done} />}
            {step === 4 && <StagesStep progress={data} onBack={back} onDone={done} />}
            {step === 5 && <CurriculumStep progress={data} onBack={back} onDone={done} />}
            {step === 6 && <TeachersStep progress={data} onBack={back} onDone={done} />}
            {step === 7 && <ReviewStep progress={data} onBack={back} onFinished={() => navigate("/", { replace: true, state: { setupFinished: true } })} />}
          </Card>
        </div>
      )}
    </div>
  );
}
