import { Save, X } from "lucide-react";
import { useId, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSaveYear, type AcademicYear } from "./yearsApi";

const text = messages.school.years;

type YearDialogProps = {
  open: boolean;
  year: AcademicYear | null;
  onClose: () => void;
  onSaved: (year: AcademicYear, message: string) => void;
  onReload: () => void;
};

/** Create or edit an academic year (label and date range). */
export function YearDialog({ open, year, onClose, onSaved, onReload }: YearDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const save = useSaveYear();

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      id: year?.id ?? null,
      input: {
        label: String(form.get("yearLabel") ?? ""),
        startDate: String(form.get("yearStart") ?? ""),
        endDate: String(form.get("yearEnd") ?? ""),
        version: year?.version ?? 0,
      },
    }, {
      onSuccess: (saved) => {
        feedback.reset();
        onSaved(saved as AcademicYear, text.saved);
      },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={year ? text.edit : text.add}
      onClose={close}
      footer={(
        <>
          <Button type="submit" form={formId} icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>
            {text.save}
          </Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>
        </>
      )}
    >
      <form id={formId} ref={feedback.formRef} key={`${year?.id ?? "new"}-${year?.version ?? 0}`} className="form-stack dialog-form" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
        <Alert tone="error" message={feedback.error} />
        <TextField id="yearLabel" label={text.label} hint={text.labelHint} defaultValue={year?.label ?? ""} maxLength={50} required field="Label" errors={feedback.fieldErrors} />
        <div className="form-grid">
          <TextField id="yearStart" type="date" label={text.startDate} defaultValue={year?.startDate ?? ""} required field="StartDate" errors={feedback.fieldErrors} />
          <TextField id="yearEnd" type="date" label={text.endDate} defaultValue={year?.endDate ?? ""} required field="EndDate" errors={feedback.fieldErrors} />
        </div>
      </form>
    </Dialog>
  );
}
