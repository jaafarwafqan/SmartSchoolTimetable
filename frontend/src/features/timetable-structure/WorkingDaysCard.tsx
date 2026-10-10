import { SectionTitle } from "../../components/ui/section-title";
import { CalendarDays, Save } from "lucide-react";
import { type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSyncedState } from "../../lib/useSyncedState";
import { useSaveWeek, type WorkingWeek } from "./scheduleApi";
import { weekdayLabel, weekdaysFrom } from "./weekdays";

const text = messages.school.scheduleStructure;

type WorkingDaysCardProps = { week: WorkingWeek; onReload: () => void };

/** Working days and week start (spec 2.3). The form re-syncs when the stored version changes. */
export function WorkingDaysCard({ week, onReload }: WorkingDaysCardProps) {
  const feedback = useFormFeedback();
  const save = useSaveWeek();
  const [days, setDays] = useSyncedState<number[]>(() => week.days, week.version);
  const [weekStart, setWeekStart] = useSyncedState(() => week.weekStartDay, week.version);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    save.mutate({ days, weekStartDay: weekStart, version: week.version }, {
      onSuccess: () => feedback.showSuccess(text.daysSaved),
      onError: feedback.showError,
    });
  }

  return (
    <Card className="page-card" aria-labelledby="working-days-title">
      <SectionTitle level={2} icon={CalendarDays} id="working-days-title">{text.workingDays}</SectionTitle>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit}>
        <fieldset className="weekday-grid" aria-invalid={feedback.fieldErrors.Days ? true : undefined} aria-describedby={feedback.fieldErrors.Days ? "days-error" : undefined}>
          <legend>{text.chooseDays}</legend>
          {weekdaysFrom(weekStart).map((day) => (
            <Checkbox
              key={day}
              name="days"
              value={String(day)}
              checked={days.includes(day)}
              data-field="Days"
              onChange={(event) => setDays(event.target.checked ? [...days, day] : days.filter((value) => value !== day))}
            >
              {weekdayLabel(day)}
            </Checkbox>
          ))}
        </fieldset>
        {feedback.fieldErrors.Days && <p id="days-error" className="ui-field-error">{feedback.fieldErrors.Days}</p>}
        <Field id="week-start" label={text.weekStart} error={feedback.fieldErrors.WeekStartDay}>
          <Select
            id="week-start"
            value={String(weekStart)}
            options={weekdaysFrom(7).map((day) => ({ value: String(day), label: weekdayLabel(day) }))}
            onChange={(event) => setWeekStart(Number(event.target.value))}
          />
        </Field>
        <div className="form-actions">
          <Button type="submit" icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.saveDays}</Button>
        </div>
      </form>
      <p className="card-note"><CalendarDays aria-hidden="true" size={16} />{text.shiftYearNote}</p>
    </Card>
  );
}
