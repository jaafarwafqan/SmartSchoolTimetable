import { Save, X } from "lucide-react";
import { useId, useState, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SelectField } from "../../components/SelectField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { Dialog } from "../../components/ui/dialog";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSaveCalendarDay, type CalendarDay, type CalendarKind } from "./calendarApi";

const text = messages.school.calendar;
export const kindOptions = (Object.keys(text.kinds) as CalendarKind[]).map((kind) => ({ value: kind, label: text.kinds[kind] }));

type CalendarDayDialogProps = {
  open: boolean;
  day: CalendarDay | null;
  defaultDate?: string;
  onClose: () => void;
  onSaved: (day: CalendarDay) => void;
  onReload: () => void;
};

/** Add or edit a calendar day or date range; dates outside the current year are allowed with a warning. */
export function CalendarDayDialog({ open, day, defaultDate, onClose, onSaved, onReload }: CalendarDayDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const save = useSaveCalendarDay();
  const [affects, setAffects] = useState(day?.affectsSchedule ?? true);

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      id: day?.id ?? null,
      input: {
        title: String(form.get("calendarTitle") ?? ""),
        startDate: String(form.get("calendarStart") ?? ""),
        endDate: String(form.get("calendarEnd") ?? "").trim() || null,
        kind: String(form.get("calendarKind") ?? "") as CalendarKind,
        affectsSchedule: affects,
        version: day?.version ?? 0,
      },
    }, {
      onSuccess: (saved) => { feedback.reset(); onSaved(saved); },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={day ? text.edit : text.add}
      onClose={close}
      footer={(
        <>
          <Button type="submit" form={formId} icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.save}</Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>
        </>
      )}
    >
      <form id={formId} ref={feedback.formRef} className="form-stack dialog-form" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
        <Alert tone="error" message={feedback.error} />
        <TextField id="calendarTitle" label={text.titleField} defaultValue={day?.title ?? ""} maxLength={120} required field="Title" errors={feedback.fieldErrors} />
        <div className="form-grid">
          <TextField id="calendarStart" type="date" label={text.startDate} defaultValue={day?.startDate ?? defaultDate ?? ""} required field="StartDate" errors={feedback.fieldErrors} />
          <TextField id="calendarEnd" type="date" label={text.endDate} hint={text.endDateHint} defaultValue={day && day.endDate !== day.startDate ? day.endDate : ""} field="EndDate" errors={feedback.fieldErrors} />
        </div>
        <SelectField id="calendarKind" label={text.kind} options={kindOptions} defaultValue={day?.kind ?? "officialHoliday"} required field="Kind" errors={feedback.fieldErrors} />
        <Checkbox checked={affects} onChange={(event) => setAffects(event.target.checked)}>{text.affectsSchedule}</Checkbox>
      </form>
    </Dialog>
  );
}
