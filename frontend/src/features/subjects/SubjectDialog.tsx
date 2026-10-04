import { Save, X } from "lucide-react";
import { useId, useState, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SelectField } from "../../components/SelectField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { Dialog } from "../../components/ui/dialog";
import { Field } from "../../components/ui/field";
import { SubjectColorPicker } from "../../components/ui/subject-color-picker";
import { Textarea } from "../../components/ui/textarea";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { BlockedPeriodsEditor, slotsInGrid } from "../timetable-structure/BlockedPeriodsEditor";
import { useScheduleGrid, type BlockedSlot } from "../timetable-structure/scheduleApi";
import { useSaveSubject, type Subject } from "./subjectsApi";

const text = messages.school.subjects;
const flagKeys = ["distributionEnabled", "spreadAcrossDays", "heavy", "requiresDoublePeriod"] as const;
type FlagKey = (typeof flagKeys)[number];

type SubjectDialogProps = { open: boolean; subject: Subject | null; onClose: () => void; onSaved: () => void; onReload: () => void };

/** Create or edit a subject: name, palette colour, priority, distribution flags, blocked periods and notes. */
export function SubjectDialog({ open, subject, onClose, onSaved, onReload }: SubjectDialogProps) {
  const formId = useId();
  const format = useFormatter();
  const feedback = useFormFeedback();
  const save = useSaveSubject();
  const grid = useScheduleGrid();
  const [color, setColor] = useState(subject?.colorIndex ?? 1);
  const [flags, setFlags] = useState<Record<FlagKey, boolean>>(() => ({
    distributionEnabled: subject?.distributionEnabled ?? true,
    spreadAcrossDays: subject?.spreadAcrossDays ?? false,
    heavy: subject?.heavy ?? false,
    requiresDoublePeriod: subject?.requiresDoublePeriod ?? false,
  }));
  const [blocked, setBlocked] = useState<BlockedSlot[]>(subject?.blockedPeriods ?? []);
  const priorities = [1, 2, 3, 4, 5].map((value) => ({ value: String(value), label: text.priorityValue(format.number(value)) }));

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      id: subject?.id ?? null,
      input: {
        name: String(form.get("subjectName") ?? ""),
        colorIndex: color,
        priority: Number(form.get("subjectPriority") || 0),
        ...flags,
        blockedPeriods: slotsInGrid(blocked, grid.data),
        notes: String(form.get("subjectNotes") ?? "").trim() || null,
        version: subject?.version ?? 0,
      },
    }, {
      onSuccess: () => { feedback.reset(); onSaved(); },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={subject ? text.edit : text.add}
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
        <TextField id="subjectName" label={text.name} defaultValue={subject?.name ?? ""} maxLength={80} required field="Name" errors={feedback.fieldErrors} />
        <SubjectColorPicker name="subjectColor" legend={text.color} value={color} swatchLabel={(index) => text.colorSwatch(format.number(index))} onChange={setColor} />
        <SelectField id="subjectPriority" label={text.priority} hint={text.priorityHint} options={priorities} defaultValue={String(subject?.priority ?? 3)} required field="Priority" errors={feedback.fieldErrors} />
        <fieldset className="weekday-grid">
          <legend>{text.flags}</legend>
          {flagKeys.map((key) => (
            <Checkbox key={key} checked={flags[key]} onChange={(event) => setFlags({ ...flags, [key]: event.target.checked })}>{text[key]}</Checkbox>
          ))}
        </fieldset>
        <BlockedPeriodsEditor id="subject-blocked" value={blocked} onChange={setBlocked} error={feedback.fieldErrors.BlockedPeriods} />
        <Field id="subjectNotes" label={text.notes} error={feedback.fieldErrors.Notes}>
          <Textarea id="subjectNotes" name="subjectNotes" defaultValue={subject?.notes ?? ""} maxLength={500} data-field="Notes" />
        </Field>
      </form>
    </Dialog>
  );
}
