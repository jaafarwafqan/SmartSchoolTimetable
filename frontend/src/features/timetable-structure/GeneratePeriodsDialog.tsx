import { WandSparkles, X } from "lucide-react";
import { useId, useState, type FormEvent } from "react";
import { TimeField } from "../../components/TimeField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useTemplateCatalog } from "../curriculum/curriculumApi";
import { useSchoolProfile } from "../school-profile/profileApi";
import { BreaksEditor, validBreaks, type BreakSlot } from "./BreaksEditor";
import { useGeneratePeriods, type Period } from "./scheduleApi";

const text = messages.school.scheduleStructure;
const presetText = messages.school.templates;

type GeneratePeriodsDialogProps = {
  open: boolean;
  yearId: number;
  /** First lesson start proposed in the form (editable). */
  defaultStart: string;
  onClose: () => void;
  onGenerated: (periods: Period[]) => void;
};

/**
 * "Generate periods" helper (spec 2.5; break model ADR 0026): lessons, then up to three editable breaks and an
 * optional gap. A preset only fills the values. The server builds an editable list; nothing is saved here.
 */
export function GeneratePeriodsDialog({ open, yearId, defaultStart, onClose, onGenerated }: GeneratePeriodsDialogProps) {
  const formId = useId();
  const feedback = useFormFeedback();
  const generate = useGeneratePeriods(yearId);
  const catalog = useTemplateCatalog().data;
  const schoolType = useSchoolProfile().data?.schoolType ?? "other";
  const suggestedBreak = catalog?.breakDefaults.minutes[schoolType] ?? 15;
  const presets = catalog?.periodPresets ?? [];
  const [presetKey, setPresetKey] = useState("");
  const [lessonCount, setLessonCount] = useState("7");
  const [breaks, setBreaks] = useState<BreakSlot[]>([{ afterLesson: 4, minutes: 15 }]);
  const [gapMinutes, setGapMinutes] = useState(0);
  const preset = presets.find((item) => item.key === presetKey) ?? null;
  const lessons = Math.max(1, Math.min(12, Number(lessonCount) || 1));

  function choosePreset(key: string) {
    setPresetKey(key);
    feedback.reset();
    const chosen = presets.find((item) => item.key === key);
    if (!chosen) return;
    setLessonCount(String(chosen.lessonCount));
    setBreaks(chosen.breaks);
    setGapMinutes(0);
  }

  function close() {
    feedback.reset();
    onClose();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    generate.mutate({
      firstStartTime: String(form.get("firstStartTime") ?? ""),
      lessonMinutes: Number(form.get("lessonMinutes") || 0),
      lessonCount: Number(lessonCount || 0),
      breakMinutes: 0,
      breakAfterLesson: null,
      breaks: validBreaks(lessons, breaks),
      gapMinutes,
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
        <div className="form-grid" key={`preset-${presetKey}`}>
          {presets.length > 0 && (
            <Field id="periodPreset" label={presetText.presetsLabel}>
              <Select id="periodPreset" value={presetKey} onChange={(event) => choosePreset(event.target.value)}
                options={[{ value: "", label: presetText.presetNone }, ...presets.map((item) => ({ value: item.key, label: item.name }))]} />
            </Field>
          )}
          <TimeField id="firstStartTime" label={text.firstStart} defaultValue={preset?.firstStart ?? defaultStart} required field="FirstStartTime" errors={feedback.fieldErrors} />
          <TextField id="lessonCount" type="number" min={1} max={12} label={text.lessonCount} value={lessonCount} onChange={(event) => setLessonCount(event.target.value)} required field="LessonCount" errors={feedback.fieldErrors} />
          <TextField id="lessonMinutes" type="number" min={10} max={120} label={text.lessonDuration} defaultValue={String(preset?.lessonMinutes ?? 45)} required field="LessonMinutes" errors={feedback.fieldErrors} />
        </div>
        <BreaksEditor idPrefix="generator" lessonCount={lessons} breaks={validBreaks(lessons, breaks)} gapMinutes={gapMinutes} suggestedMinutes={suggestedBreak}
          onChange={(next, gap) => { setBreaks(next); setGapMinutes(gap); }} />
      </form>
    </Dialog>
  );
}
