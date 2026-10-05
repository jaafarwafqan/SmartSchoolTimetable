import { Save, X } from "lucide-react";
import { useState, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SelectField } from "../../components/SelectField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
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

type SubjectEditorProps = { subject: Subject; onSaved: () => void; onCancel: () => void; onReload: () => void };

/** In-place editor of an expanded subject row: name, colour, priority; flags, blocked periods under advanced options. */
export function SubjectEditor({ subject, onSaved, onCancel, onReload }: SubjectEditorProps) {
  const prefix = `subject-${subject.id}`;
  const format = useFormatter();
  const feedback = useFormFeedback();
  const save = useSaveSubject();
  const grid = useScheduleGrid();
  const [color, setColor] = useState(subject.colorIndex);
  const [flags, setFlags] = useState<Record<FlagKey, boolean>>({
    distributionEnabled: subject.distributionEnabled,
    spreadAcrossDays: subject.spreadAcrossDays,
    heavy: subject.heavy,
    requiresDoublePeriod: subject.requiresDoublePeriod,
  });
  const [blocked, setBlocked] = useState<BlockedSlot[]>(subject.blockedPeriods);
  const priorities = [1, 2, 3, 4, 5].map((value) => ({ value: String(value), label: text.priorityValue(format.number(value)) }));

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      id: subject.id,
      input: {
        name: String(form.get(`${prefix}-name`) ?? ""),
        colorIndex: color,
        priority: Number(form.get(`${prefix}-priority`) || 0),
        ...flags,
        blockedPeriods: slotsInGrid(blocked, grid.data),
        notes: String(form.get(`${prefix}-notes`) ?? "").trim() || null,
        version: subject.version,
      },
    }, { onSuccess: () => { feedback.reset(); onSaved(); }, onError: feedback.showError });
  }

  return (
    <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="error" message={feedback.error} />
      <div className="form-grid">
        <TextField id={`${prefix}-name`} label={text.name} defaultValue={subject.name} maxLength={80} required field="Name" errors={feedback.fieldErrors} />
        <SelectField id={`${prefix}-priority`} label={text.priority} hint={text.priorityHint} options={priorities} defaultValue={String(subject.priority)} required field="Priority" errors={feedback.fieldErrors} />
      </div>
      <SubjectColorPicker name={`${prefix}-color`} legend={text.color} value={color} swatchLabel={(index) => text.colorSwatch(format.number(index))} onChange={setColor} />
      <details className="advanced-options">
        <summary>{text.advanced}</summary>
        <div className="form-stack">
          <fieldset className="weekday-grid">
            <legend>{text.flags}</legend>
            {flagKeys.map((key) => (
              <Checkbox key={key} checked={flags[key]} onChange={(event) => setFlags({ ...flags, [key]: event.target.checked })}>{text[key]}</Checkbox>
            ))}
          </fieldset>
          <BlockedPeriodsEditor id={`${prefix}-blocked`} value={blocked} onChange={setBlocked} error={feedback.fieldErrors.BlockedPeriods} />
          <Field id={`${prefix}-notes`} label={text.notes} error={feedback.fieldErrors.Notes}>
            <Textarea id={`${prefix}-notes`} name={`${prefix}-notes`} defaultValue={subject.notes ?? ""} maxLength={500} data-field="Notes" />
          </Field>
        </div>
      </details>
      <div className="form-actions">
        <Button type="submit" icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.save}</Button>
        <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={onCancel}>{messages.app.cancel}</Button>
      </div>
    </form>
  );
}
