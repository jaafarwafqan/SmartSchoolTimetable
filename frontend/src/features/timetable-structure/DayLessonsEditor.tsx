import { Save } from "lucide-react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Stepper } from "../../components/ui/stepper";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSyncedState } from "../../lib/useSyncedState";
import { useSaveDayLessons, type DayLessons, type Shift } from "./scheduleApi";
import { weekdayLabel } from "./weekdays";

const text = messages.school.scheduleStructure;

type DayLessonsEditorProps = { yearId: number; shift: Shift; onSaved: () => void; onReload: () => void };

/**
 * Lessons per day for one shift (spec 2.5 §3.1): a compact row of steppers, one per working day, 0..lesson count.
 * The weekly total is the weekly capacity of every section on this shift.
 */
export function DayLessonsEditor({ yearId, shift, onSaved, onReload }: DayLessonsEditorProps) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const save = useSaveDayLessons(yearId);
  const [counts, setCounts] = useSyncedState<DayLessons[]>(() => shift.dayLessons, shift.version);
  const total = counts.reduce((sum, day) => sum + day.lessons, 0);
  if (shift.lessonCount === 0) return null;

  return (
    <section className="day-lessons" aria-labelledby={`day-lessons-${shift.id}`}>
      <h3 id={`day-lessons-${shift.id}`}>{text.dayLessonsTitle}</h3>
      <p className="ui-field-hint">{text.dayLessonsHint}</p>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="error" message={feedback.error} />
      <ul className="day-lessons-row">
        {counts.map((entry) => (
          <li key={entry.day}>
            <span className="day-lessons-day">{weekdayLabel(entry.day)}</span>
            <Stepper
              id={`day-lessons-${shift.id}-${entry.day}`}
              label={text.dayLessonsFor(weekdayLabel(entry.day))}
              value={entry.lessons}
              min={0}
              max={shift.lessonCount}
              format={format.number}
              decreaseLabel={text.decreaseFor(weekdayLabel(entry.day))}
              increaseLabel={text.increaseFor(weekdayLabel(entry.day))}
              onChange={(lessons) => setCounts(counts.map((day) => (day.day === entry.day ? { ...day, lessons } : day)))}
            />
          </li>
        ))}
      </ul>
      <p className="day-lessons-total">{text.weeklyTotal(format.count(total, "lesson"))}</p>
      <div className="form-actions">
        <Button icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}
          onClick={() => {
            feedback.reset();
            save.mutate({ shiftId: shift.id, dayLessons: counts, version: shift.version }, { onSuccess: onSaved, onError: feedback.showError });
          }}>
          {text.saveDayLessons}
        </Button>
      </div>
    </section>
  );
}
