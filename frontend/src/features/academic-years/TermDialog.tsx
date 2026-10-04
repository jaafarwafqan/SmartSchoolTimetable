import { Save, X } from "lucide-react";
import { useId, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSaveTerm, type AcademicYear, type Term } from "./yearsApi";

const text = messages.school.years;

type TermDialogProps = {
  open: boolean;
  year: AcademicYear;
  term: Term | null;
  onClose: () => void;
  onSaved: (message: string) => void;
  onReload: () => void;
};

/** Create or edit a term; the server checks it lies inside the year and does not overlap another term. */
export function TermDialog({ open, year, term, onClose, onSaved, onReload }: TermDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const save = useSaveTerm();

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      yearId: year.id,
      termId: term?.id ?? null,
      input: {
        name: String(form.get("termName") ?? ""),
        startDate: String(form.get("termStart") ?? ""),
        endDate: String(form.get("termEnd") ?? ""),
        version: year.version,
      },
    }, {
      onSuccess: () => {
        feedback.reset();
        onSaved(text.saved);
      },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={term ? text.editTerm : text.addTerm}
      description={text.termsOf(year.label)}
      onClose={close}
      footer={(
        <>
          <Button type="submit" form={formId} icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>
            {text.saveTerm}
          </Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>
        </>
      )}
    >
      <form id={formId} ref={feedback.formRef} key={`${term?.id ?? "new"}-${year.version}`} className="form-stack dialog-form" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
        <Alert tone="error" message={feedback.error} />
        <TextField id="termName" label={text.termName} hint={text.termNameHint} defaultValue={term?.name ?? ""} maxLength={100} required field="Name" errors={feedback.fieldErrors} />
        <div className="form-grid">
          <TextField id="termStart" type="date" label={text.startDate} defaultValue={term?.startDate ?? year.startDate} min={year.startDate} max={year.endDate} required field="StartDate" errors={feedback.fieldErrors} />
          <TextField id="termEnd" type="date" label={text.endDate} defaultValue={term?.endDate ?? ""} min={year.startDate} max={year.endDate} required field="EndDate" errors={feedback.fieldErrors} />
        </div>
      </form>
    </Dialog>
  );
}
