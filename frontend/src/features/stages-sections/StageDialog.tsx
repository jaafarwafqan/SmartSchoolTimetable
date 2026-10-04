import { Save, X } from "lucide-react";
import { useId, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSaveStage, type Stage } from "./stagesApi";

const text = messages.school.stagesSections;

type StageDialogProps = {
  open: boolean;
  yearId: number;
  stage: Stage | null;
  nextOrder: number;
  onClose: () => void;
  onSaved: (stage: Stage) => void;
  onReload: () => void;
};

/** Create or edit a stage (name and display order) of the selected year. */
export function StageDialog({ open, yearId, stage, nextOrder, onClose, onSaved, onReload }: StageDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const save = useSaveStage(yearId);

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      id: stage?.id ?? null,
      input: { name: String(form.get("stageName") ?? ""), displayOrder: Number(form.get("stageOrder") || 0), version: stage?.version ?? 0 },
    }, {
      onSuccess: (saved) => { feedback.reset(); onSaved(saved); },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={stage ? text.editStage : text.addStage}
      onClose={close}
      footer={(
        <>
          <Button type="submit" form={formId} icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.saveStage}</Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>
        </>
      )}
    >
      <form id={formId} ref={feedback.formRef} key={`${stage?.id ?? "new"}-${stage?.version ?? 0}`} className="form-stack dialog-form" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
        <Alert tone="error" message={feedback.error} />
        <TextField id="stageName" label={text.stageName} defaultValue={stage?.name ?? ""} maxLength={80} required field="Name" errors={feedback.fieldErrors} />
        <TextField id="stageOrder" type="number" min={1} max={999} label={text.order} defaultValue={String(stage?.displayOrder ?? nextOrder)} required field="DisplayOrder" errors={feedback.fieldErrors} />
      </form>
    </Dialog>
  );
}
