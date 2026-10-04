import { Save, X } from "lucide-react";
import { useId, useState, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { Field } from "../../components/ui/field";
import { Textarea } from "../../components/ui/textarea";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { slotsInGrid } from "../timetable-structure/BlockedPeriodsEditor";
import { useScheduleGrid } from "../timetable-structure/scheduleApi";
import { TeacherConstraints, type ConstraintState } from "./TeacherConstraints";
import { useSaveTeacher, type Teacher } from "./teachersApi";

const text = messages.school.teachers;

type TeacherDialogProps = { open: boolean; teacher: Teacher | null; onClose: () => void; onSaved: () => void; onReload: () => void };

const optionalNumber = (value: FormDataEntryValue | null) => (String(value ?? "").trim() ? Number(value) : null);
const optionalText = (value: FormDataEntryValue | null) => String(value ?? "").trim() || null;

/** Add or edit a teacher: names, constraints (off days, blocked periods, release, limits) and notes. */
export function TeacherDialog({ open, teacher, onClose, onSaved, onReload }: TeacherDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const save = useSaveTeacher();
  const grid = useScheduleGrid();
  const [constraints, setConstraints] = useState<ConstraintState>(() => ({
    offDays: teacher?.offDays ?? [],
    blocked: teacher?.blockedPeriods ?? [],
    released: teacher?.fullyReleased ?? false,
  }));

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      id: teacher?.id ?? null,
      input: {
        fullName: String(form.get("teacherFullName") ?? ""),
        shortName: String(form.get("teacherShortName") ?? ""),
        offDays: constraints.offDays.filter((day) => grid.data?.days.includes(day)),
        blockedPeriods: slotsInGrid(constraints.blocked, grid.data),
        fullyReleased: constraints.released,
        releaseReason: optionalText(form.get("releaseReason")),
        releaseFrom: optionalText(form.get("releaseFrom")),
        releaseTo: optionalText(form.get("releaseTo")),
        maxLessonsPerDay: optionalNumber(form.get("maxPerDay")),
        maxLessonsPerWeek: optionalNumber(form.get("maxPerWeek")),
        notes: optionalText(form.get("teacherNotes")),
        version: teacher?.version ?? 0,
      },
    }, {
      onSuccess: () => { feedback.reset(); onSaved(); },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={teacher ? text.edit : text.add}
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
        <div className="form-grid">
          <TextField id="teacherFullName" label={text.fullName} defaultValue={teacher?.fullName ?? ""} maxLength={150} required field="FullName" errors={feedback.fieldErrors} />
          <TextField id="teacherShortName" label={text.shortName} hint={text.shortNameHint} defaultValue={teacher?.shortName ?? ""} maxLength={40} required field="ShortName" errors={feedback.fieldErrors} />
        </div>
        <TeacherConstraints teacher={teacher} state={constraints} onChange={setConstraints} errors={feedback.fieldErrors} />
        <Field id="teacherNotes" label={text.notes} error={feedback.fieldErrors.Notes}>
          <Textarea id="teacherNotes" name="teacherNotes" defaultValue={teacher?.notes ?? ""} maxLength={500} data-field="Notes" />
        </Field>
      </form>
    </Dialog>
  );
}
