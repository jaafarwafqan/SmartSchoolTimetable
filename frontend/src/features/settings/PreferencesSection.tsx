import { Monitor, Moon, Save, SlidersHorizontal, Sun } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { ChoiceCards, type Choice } from "../../components/ui/choice-cards";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import type { ThemePreference } from "../../lib/theme";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSyncedState } from "../../lib/useSyncedState";
import { useSchoolProfile, useUpdateProfile, type SchoolProfile } from "../school-profile/profileApi";
import { usePreferences, useSavePreferences, type Preferences, type PreferencesInput, type PrintSetting } from "./preferencesApi";
import { SettingsSection } from "./SettingsSection";

const text = messages.school.preferences;
const profileText = messages.school.profile;

const themeChoices: readonly Choice<ThemePreference>[] = [
  { value: "system", label: text.theme.system, description: text.theme.hints.system, icon: <Monitor aria-hidden="true" size={20} /> },
  { value: "light", label: text.theme.light, description: text.theme.hints.light, icon: <Sun aria-hidden="true" size={20} /> },
  { value: "dark", label: text.theme.dark, description: text.theme.hints.dark, icon: <Moon aria-hidden="true" size={20} /> },
];

type Draft = { theme: ThemePreference; semester: "auto" | "1" | "2"; section: PrintSetting; teacher: PrintSetting; school: PrintSetting; printFit: boolean; numeralSystem: string };

const toDraft = (preferences: Preferences, profile: SchoolProfile): Draft => ({
  theme: preferences.theme,
  semester: preferences.defaultSemester === null ? "auto" : (String(preferences.defaultSemester) as "1" | "2"),
  section: preferences.section,
  teacher: preferences.teacher,
  school: preferences.school,
  printFit: preferences.printFit,
  numeralSystem: profile.numeralSystem,
});

const printKinds = ["section", "teacher", "school"] as const;

function PrintRow({ kind, value, onChange }: { kind: (typeof printKinds)[number]; value: PrintSetting; onChange: (value: PrintSetting) => void }) {
  return (
    <div className="preferences-print-row">
      <strong>{text.print[kind]}</strong>
      <div className="print-options-fields">
      <Field id={`pref-${kind}-paper`} label={text.print.paper}>
        <Select id={`pref-${kind}-paper`} value={value.paper} onChange={(event) => onChange({ ...value, paper: event.target.value as PrintSetting["paper"] })}
          options={[{ value: "a4", label: "A4" }, { value: "a3", label: "A3" }]} />
      </Field>
      <Field id={`pref-${kind}-orientation`} label={text.print.orientation}>
        <Select id={`pref-${kind}-orientation`} value={value.orientation} onChange={(event) => onChange({ ...value, orientation: event.target.value as PrintSetting["orientation"] })}
          options={[{ value: "portrait", label: text.print.portrait }, { value: "landscape", label: text.print.landscape }]} />
      </Field>
      </div>
    </div>
  );
}

function PreferencesForm({ preferences, profile }: { preferences: Preferences; profile: SchoolProfile }) {
  const feedback = useFormFeedback();
  const savePreferences = useSavePreferences();
  const updateProfile = useUpdateProfile();
  const [draft, setDraft] = useSyncedState(() => toDraft(preferences, profile), preferences.version * 10_000 + profile.version);
  const set = (change: Partial<Draft>) => setDraft({ ...draft, ...change });

  function save() {
    feedback.reset();
    const input: PreferencesInput = {
      theme: draft.theme,
      defaultSemester: draft.semester === "auto" ? null : (Number(draft.semester) as 1 | 2),
      section: draft.section,
      teacher: draft.teacher,
      school: draft.school,
      printFit: draft.printFit,
      version: preferences.version,
    };
    const done = () => feedback.showSuccess(text.saved);
    const saveProfileThen = () => {
      // Digits live on the school profile: saved with the profile's own version, only when they changed.
      if (draft.numeralSystem === profile.numeralSystem) { done(); return; }
      updateProfile.mutate({
        name: profile.name, schoolType: profile.schoolType, studyType: profile.studyType, principalName: profile.principalName,
        scheduleOfficerName: profile.scheduleOfficerName, timeZone: profile.timeZone, numeralSystem: draft.numeralSystem,
        calendarDisplay: profile.calendarDisplay, version: profile.version,
      }, { onSuccess: done, onError: feedback.showError });
    };
    savePreferences.mutate(input, { onSuccess: saveProfileThen, onError: feedback.showError });
  }

  return (
    <div className="form-stack">
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <ChoiceCards name="preference-theme" legend={text.theme.legend} choices={themeChoices} value={draft.theme} onChange={(theme) => set({ theme })} />
      <div className="form-grid">
        <Field id="pref-digits" label={text.digits.label} hint={text.digits.hint}>
          <Select id="pref-digits" aria-describedby="pref-digits-hint" value={draft.numeralSystem} onChange={(event) => set({ numeralSystem: event.target.value })}
            options={profile.options.numeralSystems.map((value) => ({ value, label: profileText.numeralSystems[value as keyof typeof profileText.numeralSystems] ?? value }))} />
        </Field>
        <Field id="pref-semester" label={text.semester.label} hint={text.semester.hint}>
          <Select id="pref-semester" aria-describedby="pref-semester-hint" value={draft.semester} onChange={(event) => set({ semester: event.target.value as Draft["semester"] })}
            options={[{ value: "auto", label: text.semester.auto }, { value: "1", label: text.semester.first }, { value: "2", label: text.semester.second }]} />
        </Field>
      </div>
      <fieldset className="preferences-print">
        <legend>{text.print.legend}</legend>
        <p className="card-note">{text.print.hint}</p>
        {printKinds.map((kind) => <PrintRow key={kind} kind={kind} value={draft[kind]} onChange={(value) => set({ [kind]: value } as Partial<Draft>)} />)}
        <Checkbox checked={draft.printFit} onChange={(event) => set({ printFit: event.target.checked })}>{text.print.fit}</Checkbox>
      </fieldset>
      <div className="form-actions">
        <Button icon={<Save aria-hidden="true" size={20} />} loading={savePreferences.isPending || updateProfile.isPending} onClick={save}>{text.save}</Button>
      </div>
    </div>
  );
}

/** «التفضيلات» (M2): theme, digits, the semester the viewer opens on, and the default print options. */
export function PreferencesSection() {
  const preferences = usePreferences();
  const profile = useSchoolProfile();
  return (
    <SettingsSection id="preferences-section" icon={SlidersHorizontal} title={text.title} description={text.description}>
      {(preferences.isError || profile.isError) && <Alert tone="error" message={text.loadFailed} />}
      {(preferences.isPending || profile.isPending) && <Spinner label={messages.app.loadingContent} />}
      {preferences.data && profile.data && <PreferencesForm preferences={preferences.data} profile={profile.data} />}
    </SettingsSection>
  );
}
