import { Building2, GraduationCap, Moon, School, Sun, SunMoon } from "lucide-react";
import { useState } from "react";
import { TextField } from "../../components/TextField";
import { ChoiceCards, type Choice } from "../../components/ui/choice-cards";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSchoolProfile } from "../school-profile/profileApi";
import { WizardFooter, type WizardStep } from "./WizardFrame";
import { useSaveSchoolStep } from "./wizardApi";

const text = messages.school.wizard;
type SchoolTypeChoice = "primary" | "intermediate" | "preparatory" | "secondary";
type ModeChoice = "morning" | "evening" | "dual";
const schoolTypes: readonly SchoolTypeChoice[] = ["primary", "intermediate", "preparatory", "secondary"];
const typeIcons = { primary: <School size={20} />, intermediate: <Building2 size={20} />, preparatory: <GraduationCap size={20} />, secondary: <Building2 size={20} /> };
const modes: readonly Choice<ModeChoice>[] = [
  { value: "morning", label: text.school.modes.morning, description: text.school.modeHints.morning, icon: <Sun size={20} /> },
  { value: "evening", label: text.school.modes.evening, description: text.school.modeHints.evening, icon: <Moon size={20} /> },
  { value: "dual", label: text.school.modes.dual, description: text.school.modeHints.dual, icon: <SunMoon size={20} /> },
];
const asType = (value: string | undefined): SchoolTypeChoice | null => schoolTypes.find((type) => type === value) ?? null;
const asMode = (value: string | undefined): ModeChoice | null => modes.find((mode) => mode.value === value)?.value ?? null;

/** Step 1: name, school type (cards), shift mode (cards), optional principal. */
export function SchoolStep({ onBack, onDone }: { onBack: () => void; onDone: () => void }) {
  const profile = useSchoolProfile();
  const feedback = useFormFeedback();
  const save = useSaveSchoolStep();
  const [name, setName] = useState<string | null>(null);
  const [principal, setPrincipal] = useState<string | null>(null);
  const [type, setType] = useState<SchoolTypeChoice | null>(null);
  const [mode, setMode] = useState<ModeChoice | null>(null);
  const data = profile.data;
  const chosenType = type ?? asType(data?.schoolType);
  const chosenMode = mode ?? asMode(data?.studyType) ?? "morning";

  function next() {
    feedback.reset();
    const schoolName = (name ?? data?.name ?? "").trim();
    if (!schoolName) { feedback.showFieldErrors({ Name: messages.errors.REQUIRED }); return; }
    if (!chosenType) { feedback.showFieldErrors({ SchoolType: messages.errors.REQUIRED }); return; }
    save.mutate(
      { name: schoolName, schoolType: chosenType, shiftMode: chosenMode, principalName: (principal ?? data?.principalName ?? "").trim() || null },
      { onSuccess: onDone, onError: feedback.showError },
    );
  }

  return (
    <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={(event) => { event.preventDefault(); next(); }}>
      <TextField id="wizard-school-name" label={text.school.name} required value={name ?? data?.name ?? ""} maxLength={150}
        onChange={(event) => setName(event.target.value)} field="Name" errors={feedback.fieldErrors} />
      <ChoiceCards name="wizard-school-type" legend={text.school.type} value={chosenType} onChange={setType}
        choices={schoolTypes.map((value) => ({ value, label: messages.school.profile.schoolTypes[value], description: text.school.typeHints[value], icon: typeIcons[value] }))} />
      {feedback.fieldErrors.SchoolType && <p className="ui-field-error" role="alert">{feedback.fieldErrors.SchoolType}</p>}
      <ChoiceCards name="wizard-shift-mode" legend={text.school.shiftMode} value={chosenMode} onChange={setMode} choices={modes} />
      <TextField id="wizard-principal" label={text.school.principal} value={principal ?? data?.principalName ?? ""} maxLength={150}
        onChange={(event) => setPrincipal(event.target.value)} field="PrincipalName" errors={feedback.fieldErrors} />
      <WizardFooter step={1 satisfies WizardStep} pending={save.isPending} error={feedback.error} onBack={onBack} onNext={next} />
    </form>
  );
}
