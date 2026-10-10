import { ArrowLeft, ArrowRight, CircleCheck, Clock, ChevronsLeft } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { wizardStepCount, type SetupProgress } from "./wizardApi";

const text = messages.school.wizard;
export type WizardStep = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8;
export const wizardSteps: readonly WizardStep[] = [1, 2, 3, 4, 5, 6, 7, 8];

/** Step list with the current step (aria-current) and done/skipped marks as icon + text. */
export function WizardProgress({ step, progress, format }: { step: WizardStep; progress: SetupProgress; format: (value: number) => string }) {
  return (
    <nav aria-label={text.progressLabel} className="wizard-progress">
      <p className="wizard-progress-count">{text.stepOf(format(step), format(wizardStepCount))}</p>
      <ol className="wizard-steps">
        {wizardSteps.map((item) => {
          const done = progress.completedSteps.includes(item);
          const skipped = !done && progress.skippedSteps.includes(item);
          return (
            <li key={`wizard-step-${item}`} className={`wizard-step${item === step ? " is-current" : ""}${done ? " is-done" : ""}`} aria-current={item === step ? "step" : undefined}>
              <span className="wizard-step-marker" aria-hidden="true">{done ? <CircleCheck size={18} /> : format(item)}</span>
              <span className="wizard-step-label">{text.steps[item]}</span>
              {done && <span className="sr-only">{text.stepDone}</span>}
              {skipped && <span className="wizard-step-note">{text.stepSkipped}</span>}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

type FooterProps = {
  step: WizardStep;
  pending: boolean;
  error: string | null;
  nextLabel?: string;
  nextIcon?: ReactNode;
  onBack: () => void;
  onNext: () => void;
  onSkip?: () => void;
};

/** Back / next (saves the step) / optional skip, plus "later" back to the dashboard. Errors sit next to the step. */
export function WizardFooter({ step, pending, error, nextLabel = text.next, nextIcon, onBack, onNext, onSkip }: FooterProps) {
  return (
    <div className="wizard-footer">
      <Alert tone="error" message={error} />
      <div className="wizard-actions">
        {step > 1 && <Button variant="secondary" icon={<ArrowRight aria-hidden="true" size={20} />} onClick={onBack}>{text.back}</Button>}
        <Button icon={nextIcon ?? <ArrowLeft aria-hidden="true" size={20} />} loading={pending} onClick={onNext}>{nextLabel}</Button>
        {onSkip && <Button variant="ghost" icon={<ChevronsLeft aria-hidden="true" size={20} />} onClick={onSkip}>{text.skip}</Button>}
        <Link className="link-button wizard-later" to="/"><Clock aria-hidden="true" size={18} /><span>{text.later}</span></Link>
      </div>
    </div>
  );
}
