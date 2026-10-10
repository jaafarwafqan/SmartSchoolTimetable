import { SectionTitle } from "../../components/ui/section-title";
import { Save, School, Paintbrush } from "lucide-react";
import type { FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SelectField } from "../../components/SelectField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useUpdateProfile, type SchoolProfile } from "./profileApi";

const text = messages.school.profile;

function options<K extends string>(values: string[], labels: Record<K, string>) {
  return values.map((value) => ({ value, label: (labels as Record<string, string>)[value] ?? value }));
}

type ProfileFormProps = { profile: SchoolProfile; onReload: () => void; reloading: boolean };

/** Basic data and display preferences. Remounted (keyed by version) after a reload so values are fresh. */
export function ProfileForm({ profile, onReload, reloading }: ProfileFormProps) {
  const feedback = useFormFeedback();
  const update = useUpdateProfile();

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const value = (name: string) => String(form.get(name) ?? "");
    update.mutate({
      name: value("schoolName"),
      schoolType: value("schoolType"),
      studyType: profile.studyType,
      principalName: value("principalName") || null,
      scheduleOfficerName: value("scheduleOfficerName") || null,
      timeZone: value("timeZone"),
      numeralSystem: value("numeralSystem"),
      calendarDisplay: value("calendarDisplay"),
      version: profile.version,
    }, {
      onSuccess: () => feedback.showSuccess(text.saved),
      onError: feedback.showError,
    });
  }

  return (
    <form ref={feedback.formRef} className="form-stack form-wide" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
      {feedback.conflict && <ConflictAlert onReload={onReload} loading={reloading} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <SectionTitle level={2} icon={School}>{text.detailsTitle}</SectionTitle>
      <TextField id="schoolName" label={text.name} defaultValue={profile.name} maxLength={200} required field="Name" errors={feedback.fieldErrors} />
      <div className="form-grid">
        <SelectField id="schoolType" label={text.schoolType} options={options(profile.options.schoolTypes, text.schoolTypes)} defaultValue={profile.schoolType} required field="SchoolType" errors={feedback.fieldErrors} />
        <TextField id="principalName" label={text.principalName} defaultValue={profile.principalName ?? ""} maxLength={150} field="PrincipalName" errors={feedback.fieldErrors} />
        <TextField id="scheduleOfficerName" label={text.scheduleOfficerName} defaultValue={profile.scheduleOfficerName ?? ""} maxLength={150} field="ScheduleOfficerName" errors={feedback.fieldErrors} />
      </div>
      <SectionTitle level={2} icon={Paintbrush}>{text.displayTitle}</SectionTitle>
      <div className="form-grid">
        <SelectField id="timeZone" label={text.timeZone} options={options(profile.options.timeZones, text.timeZones)} defaultValue={profile.timeZone} required field="TimeZone" errors={feedback.fieldErrors} />
        <SelectField id="numeralSystem" label={text.numeralSystem} options={options(profile.options.numeralSystems, text.numeralSystems)} defaultValue={profile.numeralSystem} required field="NumeralSystem" errors={feedback.fieldErrors} />
        <SelectField id="calendarDisplay" label={text.calendarDisplay} options={options(profile.options.calendarDisplays, text.calendarDisplays)} defaultValue={profile.calendarDisplay} required field="CalendarDisplay" errors={feedback.fieldErrors} />
      </div>
      <div className="form-actions">
        <Button type="submit" icon={<Save aria-hidden="true" size={20} />} loading={update.isPending} disabled={feedback.conflict}>
          {text.save}
        </Button>
      </div>
    </form>
  );
}
