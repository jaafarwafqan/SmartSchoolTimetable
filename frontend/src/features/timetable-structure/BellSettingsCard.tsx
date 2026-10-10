import { SectionTitle } from "../../components/ui/section-title";
import { BellRing, Info, Save, Volume2, Bell } from "lucide-react";
import { useState, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSyncedState } from "../../lib/useSyncedState";
import { useSaveBell, type BellSettings, type BellTone } from "./scheduleApi";
import { previewTone } from "./tonePreview";

const text = messages.school.scheduleStructure;
const toneOptions: readonly { value: BellTone; label: string }[] = [
  { value: "classic", label: text.tones.classic },
  { value: "chime", label: text.tones.chime },
  { value: "beeps", label: text.tones.beeps },
  { value: "soft", label: text.tones.soft },
];

type BellSettingsCardProps = { settings: BellSettings; onReload: () => void };

/** Tone and break-bell configuration with a local Web Audio preview (ringing itself is Phase 7). */
export function BellSettingsCard({ settings, onReload }: BellSettingsCardProps) {
  const feedback = useFormFeedback();
  const save = useSaveBell();
  const [tone, setTone] = useSyncedState<BellTone>(() => settings.tone, settings.version);
  const [breakBell, setBreakBell] = useSyncedState(() => settings.breakBell, settings.version);
  const [soundFailed, setSoundFailed] = useState(false);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    save.mutate({ tone, breakBell, version: settings.version }, {
      onSuccess: () => feedback.showSuccess(text.bellSaved),
      onError: feedback.showError,
    });
  }

  function test() {
    setSoundFailed(false);
    previewTone(tone).catch(() => setSoundFailed(true));
  }

  return (
    <Card className="page-card" aria-labelledby="bell-title">
      <div className="card-header-row">
        <SectionTitle level={2} icon={Bell} id="bell-title">{text.bellSettings}</SectionTitle>
        <BellRing aria-hidden="true" size={20} />
      </div>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {soundFailed && <Alert tone="error" message={text.soundUnavailable} />}
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit}>
        <Field id="bell-tone" label={text.tone} error={feedback.fieldErrors.Tone}>
          <Select id="bell-tone" value={tone} options={toneOptions} data-field="Tone" onChange={(event) => setTone(event.target.value as BellTone)} />
        </Field>
        <Checkbox checked={breakBell} onChange={(event) => setBreakBell(event.target.checked)}>{text.breakBell}</Checkbox>
        <div className="form-actions">
          <Button type="submit" icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.saveBell}</Button>
          <Button variant="secondary" icon={<Volume2 aria-hidden="true" size={20} />} onClick={test}>{text.testSound}</Button>
        </div>
      </form>
      <p className="card-note"><Info aria-hidden="true" size={16} />{text.previewOnly}</p>
    </Card>
  );
}
