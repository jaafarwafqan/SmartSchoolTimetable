import { Coffee, Plus, Save, WandSparkles } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { EmptyState } from "../../components/ui/empty-state";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSyncedState } from "../../lib/useSyncedState";
import { GeneratePeriodsDialog } from "./GeneratePeriodsDialog";
import { PeriodRow } from "./PeriodRow";
import { lessonNumbers, newRow, periodErrors, type PeriodErrors } from "./periodRows";
import { useSavePeriods, type PeriodInput, type Shift } from "./scheduleApi";

const text = messages.school.scheduleStructure;

const toInput = ({ kind, startTime, endTime, startBell, endBell }: PeriodInput): PeriodInput => ({ kind, startTime, endTime, startBell, endBell });

type PeriodsEditorProps = { yearId: number; shift: Shift; onReload: () => void; onSaved: () => void };

/**
 * Editable daily schedule of one shift. Rows change locally until "save"; the server validates order, overlaps,
 * time ranges and lesson count, and each error is shown on its own row. Rows re-sync from the server when the
 * stored version changes and nothing is unsaved.
 */
export function PeriodsEditor({ yearId, shift, onReload, onSaved }: PeriodsEditorProps) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const save = useSavePeriods(yearId);
  const [dirty, setDirty] = useState(false);
  const [rows, setRows] = useSyncedState<PeriodInput[]>(() => shift.periods.map(toInput), shift.version, dirty);
  const [errors, setErrors] = useState<PeriodErrors | null>(null);
  const [generating, setGenerating] = useState(false);
  const numbers = lessonNumbers(rows);

  function update(next: PeriodInput[]) {
    setRows(next);
    setDirty(true);
    setErrors(null);
    feedback.reset();
  }

  function submit() {
    feedback.reset();
    setErrors(null);
    save.mutate({ shiftId: shift.id, periods: rows, version: shift.version }, {
      onSuccess: () => { setDirty(false); onSaved(); },
      onError: (reason) => {
        const parsed = periodErrors(reason);
        if (parsed) {
          setErrors(parsed);
          feedback.setError(messages.errors.VALIDATION_FAILED);
        } else {
          feedback.showError(reason);
        }
      },
    });
  }

  return (
    <section className="periods-editor" aria-labelledby="periods-title">
      <div className="card-header-row">
        <h3 id="periods-title">{text.periodsFor(shift.name)}</h3>
        <Button variant="secondary" icon={<WandSparkles aria-hidden="true" size={20} />} onClick={() => setGenerating(true)}>{text.generate}</Button>
      </div>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={[feedback.error, errors?.list].filter(Boolean).join(" ") || null} />
      {dirty && <Alert tone="warning" message={text.unsaved} />}
      {rows.length === 0
        ? <EmptyState icon={<Coffee aria-hidden="true" size={24} />} message={text.noPeriods} />
        : (
          <ol className="period-list">
            {rows.map((row, index) => (
              <PeriodRow
                key={index}
                index={index}
                row={row}
                lessonNumber={numbers[index]}
                errors={errors?.rows[index]}
                format={format}
                onChange={(changed) => update(rows.map((item, position) => (position === index ? changed : item)))}
                onRemove={() => update(rows.filter((_, position) => position !== index))}
              />
            ))}
          </ol>
        )}
      <div className="form-actions">
        <Button icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict} onClick={submit}>{text.savePeriods}</Button>
        <Button variant="secondary" icon={<Plus aria-hidden="true" size={20} />} onClick={() => update([...rows, newRow(rows, "lesson")])}>{text.addLesson}</Button>
        <Button variant="secondary" icon={<Coffee aria-hidden="true" size={20} />} onClick={() => update([...rows, newRow(rows, "break")])}>{text.addBreak}</Button>
      </div>
      <GeneratePeriodsDialog
        open={generating}
        yearId={yearId}
        onClose={() => setGenerating(false)}
        onGenerated={(periods) => { setGenerating(false); update(periods.map(toInput)); feedback.showSuccess(text.generated); }}
      />
    </section>
  );
}
