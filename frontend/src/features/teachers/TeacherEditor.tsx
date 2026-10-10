import { Save, X } from "lucide-react";
import { useState, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Textarea } from "../../components/ui/textarea";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSubjects } from "../subjects/subjectsApi";
import { slotsInGrid } from "../timetable-structure/BlockedPeriodsEditor";
import { useScheduleGrid } from "../timetable-structure/scheduleApi";
import { TeacherConstraints, type ConstraintState } from "./TeacherConstraints";
import { useSaveTeacher, type Teacher } from "./teachersApi";

const text = messages.school.teachers;
const optionalNumber = (value: FormDataEntryValue | null) => (String(value ?? "").trim() ? Number(value) : null);
const optionalText = (value: FormDataEntryValue | null) => String(value ?? "").trim() || null;

type TeacherEditorProps = { teacher: Teacher; onSaved: () => void; onCancel: () => void; onReload: () => void };

/** In-place editor of an expanded teacher row: names, constraints (off days, blocked periods, release, limits), notes. */
export function TeacherEditor({ teacher, onSaved, onCancel, onReload }: TeacherEditorProps) {
  const prefix = `teacher-${teacher.id}`;
  const feedback = useFormFeedback();
  const save = useSaveTeacher();
  const grid = useScheduleGrid();
  const [constraints, setConstraints] = useState<ConstraintState>({
    offDays: teacher.offDays,
    blocked: teacher.blockedPeriods,
    released: teacher.fullyReleased,
  });
  const format = useFormatter();
  const subjects = useSubjects({ search: "", page: 1, pageSize: 100, includeArchived: false });
  // Archived subjects stay in the list without being shown, so saving never drops them silently.
  const [specializations, setSpecializations] = useState<number[]>(teacher.specializationIds);
  const toggleSpecialization = (id: number, on: boolean) =>
    setSpecializations(on ? [...specializations, id] : specializations.filter((item) => item !== id));
  const orphans = grid.data ? constraints.blocked.length - slotsInGrid(constraints.blocked, grid.data).length : 0;

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const value = (name: string) => form.get(`${prefix}-${name}`);
    save.mutate({
      id: teacher.id,
      input: {
        fullName: String(value("fullName") ?? ""),
        shortName: String(value("shortName") ?? ""),
        offDays: constraints.offDays.filter((day) => grid.data?.days.includes(day)),
        blockedPeriods: slotsInGrid(constraints.blocked, grid.data),
        fullyReleased: constraints.released,
        releaseReason: optionalText(value("releaseReason")),
        releaseFrom: optionalText(value("releaseFrom")),
        releaseTo: optionalText(value("releaseTo")),
        maxLessonsPerDay: optionalNumber(value("maxPerDay")),
        maxLessonsPerWeek: optionalNumber(value("maxPerWeek")),
        notes: optionalText(value("notes")),
        specializationIds: specializations,
        version: teacher.version,
      },
    }, { onSuccess: () => { feedback.reset(); onSaved(); }, onError: feedback.showError });
  }

  return (
    <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="error" message={feedback.error} />
      <div className="form-grid">
        <TextField id={`${prefix}-fullName`} label={text.fullName} defaultValue={teacher.fullName} maxLength={150} required field="FullName" errors={feedback.fieldErrors} />
        <TextField id={`${prefix}-shortName`} label={text.shortName} hint={text.shortNameHint} defaultValue={teacher.shortName} maxLength={40} required field="ShortName" errors={feedback.fieldErrors} />
      </div>
      <fieldset className="choice-group weekday-grid" aria-describedby={`${prefix}-specializations-hint`}>
        <legend>{messages.school.specializations.legend}</legend>
        <p id={`${prefix}-specializations-hint`} className="card-note">{messages.school.specializations.hint}</p>
        {subjects.isSuccess && subjects.data.items.length === 0 && <p className="card-note">{messages.school.specializations.noSubjects}</p>}
        {subjects.data?.items.map((subject) => (
          <Checkbox key={subject.id} checked={specializations.includes(subject.id)} onChange={(event) => toggleSpecialization(subject.id, event.target.checked)}>
            {subject.name}
          </Checkbox>
        ))}
      </fieldset>
      {orphans > 0 && <Alert tone="warning" message={messages.school.orphanOnSave(format.number(orphans))} />}
      <TeacherConstraints prefix={prefix} teacher={teacher} state={constraints} onChange={setConstraints} errors={feedback.fieldErrors} />
      <Field id={`${prefix}-notes`} label={text.notes} error={feedback.fieldErrors.Notes}>
        <Textarea id={`${prefix}-notes`} name={`${prefix}-notes`} defaultValue={teacher.notes ?? ""} maxLength={500} data-field="Notes" />
      </Field>
      <div className="form-actions">
        <Button type="submit" icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.save}</Button>
        <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={onCancel}>{messages.app.cancel}</Button>
      </div>
    </form>
  );
}
