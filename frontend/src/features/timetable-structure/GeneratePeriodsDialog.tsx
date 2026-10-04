import { WandSparkles, X } from "lucide-react";
import { useId, type FormEvent } from "react";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useGeneratePeriods, type Period } from "./scheduleApi";

const text = messages.school.scheduleStructure;

type GeneratePeriodsDialogProps = {
  open: boolean;
  yearId: number;
  onClose: () => void;
  onGenerated: (periods: Period[]) => void;
};

/** "Generate periods" helper (spec 2.5): the server builds an editable list; nothing is saved here. */
export function GeneratePeriodsDialog({ open, yearId, onClose, onGenerated }: GeneratePeriodsDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const generate = useGeneratePeriods(yearId);

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const number = (name: string) => Number(form.get(name) || 0);
    const breakAfter = String(form.get("breakAfterLesson") ?? "").trim();
    generate.mutate({
      firstStartTime: String(form.get("firstStartTime") ?? ""),
      lessonMinutes: number("lessonMinutes"),
      lessonCount: number("lessonCount"),
      breakMinutes: number("breakMinutes"),
      breakAfterLesson: breakAfter ? Number(breakAfter) : null,
    }, {
      onSuccess: (result) => { feedback.reset(); onGenerated(result.periods); },
      onError: feedback.showError,
    });
  }

  return (
    <Dialog
      open={open}
      title={text.generate}
      description={text.generateHint}
      onClose={close}
      footer={(
        <>
          <Button type="submit" form={formId} icon={<WandSparkles aria-hidden="true" size={20} />} loading={generate.isPending}>{text.createList}</Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>
        </>
      )}
    >
      <form id={formId} ref={feedback.formRef} className="form-stack dialog-form" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <Alert tone="error" message={feedback.error} />
        <div className="form-grid">
          <TextField id="firstStartTime" type="time" label={text.firstStart} defaultValue="08:00" required field="FirstStartTime" errors={feedback.fieldErrors} />
          <TextField id="lessonCount" type="number" min={1} max={12} label={text.lessonCount} defaultValue="7" required field="LessonCount" errors={feedback.fieldErrors} />
          <TextField id="lessonMinutes" type="number" min={10} max={120} label={text.lessonDuration} defaultValue="45" required field="LessonMinutes" errors={feedback.fieldErrors} />
          <TextField id="breakAfterLesson" type="number" min={1} max={11} label={text.breakAfter} defaultValue="4" field="BreakAfterLesson" errors={feedback.fieldErrors} />
          <TextField id="breakMinutes" type="number" min={5} max={120} label={text.breakDuration} defaultValue="15" field="BreakMinutes" errors={feedback.fieldErrors} />
        </div>
      </form>
    </Dialog>
  );
}
