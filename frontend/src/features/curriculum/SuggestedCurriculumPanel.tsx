import { Check, CircleAlert, ClipboardList, RotateCcw } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useApplySuggested, usePreviewStageReset, useResetStage, useSuggestedPreview, type SuggestedPlan, type SuggestedStage } from "./curriculumApi";

const text = messages.school.suggested;

/** The review warning: icon + the full Arabic sentence (never colour alone). */
export function ReviewWarning({ short = false }: { short?: boolean }) {
  return (
    <span className="review-warning" title={text.review}>
      <CircleAlert aria-hidden="true" size={14} />
      <span>{short ? text.reviewShort : text.review}</span>
      {short && <span className="sr-only">{text.review}</span>}
    </span>
  );
}

/**
 * "تعبئة المنهج" (ADR 0028, 0029): a preview of what the official study plan 2026-2027 adds to the school's existing
 * stages (matched subjects, optional subjects unchecked, official against enabled totals, the source's review notes),
 * then an idempotent, non-destructive apply. Each stage can be reset to the plan after a before/after confirmation.
 */
export function SuggestedCurriculumPanel({ yearId, open = false }: { yearId: number; open?: boolean }) {
  const { count, number } = useFormatter();
  const feedback = useFormFeedback();
  const [optional, setOptional] = useState<ReadonlySet<string>>(new Set());
  const chosen = [...optional];
  const preview = useSuggestedPreview(yearId, chosen);
  const apply = useApplySuggested(yearId);
  const resetPreview = usePreviewStageReset(yearId);
  const reset = useResetStage(yearId);
  const [resetting, setResetting] = useState<{ stage: SuggestedStage; plan: SuggestedPlan } | null>(null);
  const plan = preview.data;
  const toAdd = (stage: SuggestedStage) => stage.entries.filter((line) => line.action === "create").length;

  function toggle(subject: string) {
    setOptional((current) => {
      const next = new Set(current);
      if (!next.delete(subject)) next.add(subject);
      return next;
    });
    feedback.reset();
  }

  function startReset(stage: SuggestedStage) {
    feedback.reset();
    resetPreview.mutate({ stageId: stage.stageId, optionalSubjects: chosen }, {
      onSuccess: (result) => setResetting({ stage, plan: result }),
      onError: feedback.showError,
    });
  }

  const resetLines = resetting?.plan.stages[0]?.entries.filter((line) => line.action === "update" || line.action === "create") ?? [];
  const optionalLine = (subject: string) => plan?.subjects.find((line) => line.name === subject);

  return (
    <details className="advanced-options tool-panel suggested-panel" open={open || undefined}>
      <summary>
        <ClipboardList aria-hidden="true" size={20} />
        <span>{text.open}</span>
        <Badge tone="primary">{text.badge}</Badge>
      </summary>
      <div className="form-stack">
        <Alert tone="info" message={text.provenance} />
        <p className="card-note">{text.description}</p>
        {plan && plan.optionalSubjects.length > 0 && (
          <fieldset className="choice-group">
            <legend>{text.optional}</legend>
            {plan.optionalSubjects.map((subject, index) => (
              <div key={`optional-${subject}`} className="optional-subject">
                <Checkbox checked={optional.has(subject)} onChange={() => toggle(subject)} aria-describedby={`optional-note-${index}`}>{subject}</Checkbox>
                <span id={`optional-note-${index}`} className="card-note">{optionalLine(subject)?.inStatedTotal ? text.optionalCounted : text.optionalExtra}</span>
              </div>
            ))}
          </fieldset>
        )}
        {preview.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
        <Alert tone="success" message={feedback.success} />
        <Alert tone="error" message={feedback.error} />
        {plan && plan.stages.length === 0 && <Alert tone="info" message={text.noStages} />}
        {plan && plan.stages.length > 0 && (
          <>
            <section aria-labelledby="suggested-subjects-title" className="plan-preview">
              <h4 id="suggested-subjects-title">{text.subjects}</h4>
              <ul className="chip-list">
                {plan.subjects.filter((line) => line.included).map((line) => (
                  <li key={`suggested-subject-${line.name}`} className={`subject-chip${line.action === "exists" ? " is-added" : ""}`}>
                    <span className="subject-chip-name">{line.name}</span>
                    <Badge tone={line.action === "exists" ? "success" : "primary"}>
                      {line.action === "create" ? text.subjectCreate : line.existingName && line.existingName !== line.name ? text.subjectAlias(line.existingName) : text.subjectExists}
                    </Badge>
                  </li>
                ))}
              </ul>
            </section>
            <ul className="suggested-stages">
              {plan.stages.map((stage) => (
                <li key={`suggested-stage-${stage.stageId}`} className="suggested-stage">
                  <span className="suggested-stage-line">{text.stageLine(stage.stageName, count(stage.currentTotal, "lesson"), count(stage.resultingTotal, "lesson"))}</span>
                  {toAdd(stage) > 0 && <span className="card-note">{text.entriesToAdd(count(toAdd(stage), "line"))}</span>}
                  <span className="suggested-totals">
                    <span>{text.officialTotal(count(stage.statedTotal, "lesson"))}</span>
                    <span>{text.enabledTotal(count(stage.suggestedTotal, "lesson"))}</span>
                  </span>
                  {stage.needsReview && <ReviewWarning />}
                  {stage.verificationNote && <p className="suggested-note">{stage.verificationNote}</p>}
                  {stage.suggestedTotal !== stage.officialTotal && <p className="suggested-note">{text.totalDiffers}</p>}
                  <Button variant="ghost" size="sm" icon={<RotateCcw aria-hidden="true" size={16} />} loading={resetPreview.isPending && resetPreview.variables?.stageId === stage.stageId}
                    onClick={() => startReset(stage)}>{text.reset}</Button>
                </li>
              ))}
            </ul>
            {plan.changes === 0 ? <p className="card-note">{text.nothing}</p> : (
              <div className="form-actions">
                <Button icon={<Check aria-hidden="true" size={20} />} loading={apply.isPending}
                  onClick={() => { feedback.reset(); apply.mutate({ optionalSubjects: chosen }, { onSuccess: (result) => feedback.showSuccess(text.applied(number(result.changes))), onError: feedback.showError }); }}>
                  {text.apply}
                </Button>
              </div>
            )}
          </>
        )}
      </div>
      <ConfirmDialog
        open={resetting !== null}
        title={text.resetTitle}
        consequence={resetLines.length === 0 ? text.resetNone : [text.resetConsequence(resetting?.stage.stageName ?? ""), ...resetLines.map((line) =>
          text.resetLine(line.subject, line.currentLessons === null ? text.empty : number(line.currentLessons), number(line.lessons)))].join(" ")}
        confirmLabel={text.resetConfirm}
        confirmIcon={<RotateCcw aria-hidden="true" size={20} />}
        loading={reset.isPending}
        onCancel={() => setResetting(null)}
        onConfirm={() => {
          if (!resetting || resetLines.length === 0) { setResetting(null); return; }
          const stage = resetting.stage;
          reset.mutate({ stageId: stage.stageId, optionalSubjects: chosen }, {
            onSuccess: () => { setResetting(null); feedback.showSuccess(text.resetDone(stage.stageName)); },
            onError: (reason) => { setResetting(null); feedback.showError(reason); },
          });
        }}
      />
    </details>
  );
}
