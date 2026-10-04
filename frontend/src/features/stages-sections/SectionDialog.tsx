import { Save, X } from "lucide-react";
import { useId, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SelectField } from "../../components/SelectField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import type { SelectOption } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSaveSection, type Section } from "./stagesApi";

const text = messages.school.stagesSections;

type SectionDialogProps = {
  open: boolean;
  yearId: number;
  stageId: number;
  section: Section | null;
  shiftOptions: readonly SelectOption[];
  onClose: () => void;
  onSaved: () => void;
  onReload: () => void;
};

/** Create or edit a section: label, shift (decides its weekly capacity) and optional student count. */
export function SectionDialog({ open, yearId, stageId, section, shiftOptions, onClose, onSaved, onReload }: SectionDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const save = useSaveSection(yearId, stageId);

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const students = String(form.get("sectionStudents") ?? "").trim();
    save.mutate({
      id: section?.id ?? null,
      input: {
        label: String(form.get("sectionLabel") ?? ""),
        shiftId: Number(form.get("sectionShift") || 0),
        studentCount: students ? Number(students) : null,
        version: section?.version ?? 0,
      },
    }, {
      onSuccess: () => { feedback.reset(); onSaved(); },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={section ? text.editSection : text.addSection}
      description={text.capacityHint}
      onClose={close}
      footer={(
        <>
          <Button type="submit" form={formId} icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.saveSection}</Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>
        </>
      )}
    >
      <form id={formId} ref={feedback.formRef} key={`${section?.id ?? "new"}-${section?.version ?? 0}`} className="form-stack dialog-form" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
        <Alert tone="error" message={feedback.error} />
        <TextField id="sectionLabel" label={text.label} defaultValue={section?.label ?? ""} maxLength={40} required field="Label" errors={feedback.fieldErrors} />
        <SelectField id="sectionShift" label={text.shift} options={shiftOptions} defaultValue={String(section?.shiftId ?? shiftOptions[0]?.value ?? "")} required field="ShiftId" errors={feedback.fieldErrors} />
        <TextField id="sectionStudents" type="number" min={0} max={200} label={text.students} defaultValue={section?.studentCount === null || !section ? "" : String(section.studentCount)} field="StudentCount" errors={feedback.fieldErrors} />
      </form>
    </Dialog>
  );
}
