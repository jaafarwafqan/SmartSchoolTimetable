import { Save, X } from "lucide-react";
import { useId, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SelectField } from "../../components/SelectField";
import { DateField } from "../../components/DateField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { ltrRuns } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useAcademicYears, useSaveYear, type AcademicYear } from "./yearsApi";

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
  const years = useAcademicYears({ search: "", page: 1, pageSize: 100 });
  const copyOptions = [{ value: "", label: text.noCopy }, ...(years.data?.items ?? []).map((item) => ({ value: String(item.id), label: ltrRuns(item.label) }))];

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
        copyStructureFromYearId: year ? null : Number(form.get("yearCopyFrom")) || null,
      },
    }, {
      onSuccess: (saved) => {
        feedback.reset();
        const created = saved as AcademicYear;
        onSaved(created, created.holidaysAdded ? `${text.saved} ${text.holidaysAdded}` : text.saved);
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
          <DateField id="yearStart" label={text.startDate} defaultValue={year?.startDate ?? ""} required field="StartDate" errors={feedback.fieldErrors} />
          <DateField id="yearEnd" label={text.endDate} defaultValue={year?.endDate ?? ""} required field="EndDate" errors={feedback.fieldErrors} />
        </div>
        {!year && copyOptions.length > 1 && (
          <SelectField id="yearCopyFrom" label={text.copyFrom} hint={text.copyHint} options={copyOptions} defaultValue="" field="CopyStructureFromYearId" errors={feedback.fieldErrors} />
        )}
      </form>
    </Dialog>
  );
}
