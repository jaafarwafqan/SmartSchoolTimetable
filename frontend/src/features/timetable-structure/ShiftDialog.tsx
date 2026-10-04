import { Save, X } from "lucide-react";
import { useId, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSaveShift, type Shift } from "./scheduleApi";

const text = messages.school.scheduleStructure;

type ShiftDialogProps = {
  open: boolean;
  yearId: number;
  shift: Shift | null;
  nextOrder: number;
  onClose: () => void;
  onSaved: (shift: Shift) => void;
  onReload: () => void;
};

/** Create or rename a shift of the selected year. */
export function ShiftDialog({ open, yearId, shift, nextOrder, onClose, onSaved, onReload }: ShiftDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const save = useSaveShift(yearId);

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      id: shift?.id ?? null,
      input: { name: String(form.get("shiftName") ?? ""), displayOrder: Number(form.get("shiftOrder") || 0), version: shift?.version ?? 0 },
    }, {
      onSuccess: (saved) => { feedback.reset(); onSaved(saved); },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={shift ? text.editShift : text.addShift}
      onClose={close}
      footer={(
        <>
          <Button type="submit" form={formId} icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.saveShift}</Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>
        </>
      )}
    >
      <form id={formId} ref={feedback.formRef} key={`${shift?.id ?? "new"}-${shift?.version ?? 0}`} className="form-stack dialog-form" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
        <Alert tone="error" message={feedback.error} />
        <TextField id="shiftName" label={text.shiftName} defaultValue={shift?.name ?? ""} maxLength={50} required field="Name" errors={feedback.fieldErrors} />
        <TextField id="shiftOrder" type="number" min={1} max={99} label={text.displayOrder} defaultValue={String(shift?.displayOrder ?? nextOrder)} required field="DisplayOrder" errors={feedback.fieldErrors} />
      </form>
    </Dialog>
  );
}
