import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useTemplateCatalog, type OfficialStageTotal, type TemplateCatalog } from "../curriculum/curriculumApi";
import { useShifts, useWorkingWeek } from "../timetable-structure/scheduleApi";
import { useSessionPlan } from "../timetable-structure/sessionPlanApi";
import { initialValue, toCommand, type ShiftSystemValue } from "../timetable-structure/shiftSystem";
import { useShiftSystem } from "../timetable-structure/shiftSystemApi";
import { hasBreakIssues, LegacyConversion } from "../timetable-structure/ShiftSystemCard";
import { ShiftSystemEditor } from "../timetable-structure/ShiftSystemEditor";
import { weekdayLabel, weekdaysFrom } from "../timetable-structure/weekdays";
import { weeklyLessons } from "./timingPlan";
import { WizardFooter } from "./WizardFrame";
import { useSaveTimingStep, type SetupProgress } from "./wizardApi";

const text = messages.school.wizard;
const customDays = "custom";
type Kind = "morning" | "evening";

/**
 * A notice that never blocks: for the school type's stages with optional subjects, the total once they are all ticked
 * and the lessons a day it needs; stages above this shift's weekly lessons are named.
 */
function OptionalCapacity({ kind, stages, capacity, days }: { kind: Kind; stages: OfficialStageTotal[]; capacity: number; days: number }) {
  const format = useFormatter();
  const withOptional = stages.filter((stage) => stage.allOptionalTotal > stage.officialTotal);
  if (withOptional.length === 0 || days === 0) return null;
  const above = withOptional.filter((stage) => stage.allOptionalTotal > capacity);
  return (
    <div className="form-stack optional-capacity">
      <Alert tone={above.length > 0 ? "warning" : "info"} message={above.length > 0 ? text.timing.optionalAboveCount(format.count(above.length, "stage")) : text.timing.optionalFits} />
      <details className="advanced-options">
        <summary>{text.timing.optionalTitle}</summary>
        <ul className="optional-capacity-list">
          {withOptional.map((stage) => (
            <li key={`${kind}-optional-${stage.key}`} className={stage.allOptionalTotal > capacity ? "is-above" : undefined}>
              {text.timing.optionalLine(stage.name, format.count(stage.allOptionalTotal, "lesson"), format.count(Math.ceil(stage.allOptionalTotal / days), "lesson"))}
              {stage.allOptionalTotal > capacity && ` ${text.timing.optionalAbove(format.count(capacity, "lesson"))}`}
            </li>
          ))}
        </ul>
      </details>
    </div>
  );
}

/** The system and timings, once what is stored has loaded (the editor then keeps its own state). */
function SystemForm({ progress, days, weekStartDay, onBack, onDone }: { progress: SetupProgress; days: number[]; weekStartDay: number; onBack: () => void; onDone: () => void }) {
  const feedback = useFormFeedback();
  const save = useSaveTimingStep();
  const catalog = useTemplateCatalog();
  const context = useSchoolContext();
  const yearId = context.data?.currentYear?.id ?? null;
  const state = useShiftSystem();
  const shifts = useShifts(yearId);
  const plan = useSessionPlan();
  const ready = state.data && (yearId === null || shifts.data) && (yearId === null || plan.data);
  if (!ready) return <Spinner label={messages.app.loadingContent} />;
  if (state.data!.legacy) return <LegacyConversion state={state.data!} />;
  return (
    <SystemEditorForm key={`${state.data!.system}-${shifts.data?.items[0]?.version ?? 0}-${plan.data?.version ?? 0}`}
      start={initialValue(progress.shiftMode === "dual" ? "dual" : progress.shiftMode, shifts.data?.items[0], plan.data, days)}
      days={days} weekStartDay={weekStartDay} progress={progress} catalog={catalog.data} feedback={feedback} save={save} onBack={onBack} onDone={onDone} />
  );
}

function SystemEditorForm({ start, days, weekStartDay, progress, catalog, feedback, save, onBack, onDone }: {
  start: ShiftSystemValue; days: number[]; weekStartDay: number; progress: SetupProgress; catalog: TemplateCatalog | undefined;
  feedback: ReturnType<typeof useFormFeedback>; save: ReturnType<typeof useSaveTimingStep>; onBack: () => void; onDone: () => void;
}) {
  const [value, setValue] = useState(start);
  return (
    <>
      <ShiftSystemEditor idPrefix="wizard" value={value} days={days} presets={catalog?.periodPresets ?? []}
        suggestedBreak={catalog?.breakDefaults.minutes[progress.schoolType] ?? 15} onChange={(next) => { feedback.reset(); setValue(next); }} />
      <OptionalCapacity kind={value.system === "evening" ? "evening" : "morning"}
        stages={(catalog?.officialStages ?? []).filter((stage) => stage.schoolTypes.includes(progress.schoolType))}
        capacity={weeklyLessons(value.main, days)} days={days.length} />
      <WizardFooter step={3} pending={save.isPending} error={feedback.error} onBack={onBack}
        onNext={() => {
          feedback.reset();
          if (hasBreakIssues(value)) {
            feedback.setError(messages.school.scheduleStructure.breaks.blocked);
            return;
          }
          save.mutate({ days, weekStartDay, ...toCommand(value, days) }, { onSuccess: onDone, onError: feedback.showError });
        }} />
    </>
  );
}

/** Step 3: working days, then one block per shift of the chosen mode with a live period preview. */
export function TimingStep({ progress, onBack, onDone }: { progress: SetupProgress; onBack: () => void; onDone: () => void }) {
  const catalog = useTemplateCatalog();
  const week = useWorkingWeek();
  const context = useSchoolContext();
  const yearId = context.data?.currentYear?.id ?? null;
  const shifts = useShifts(yearId);
  const [daysChoice, setDaysChoice] = useState<string | null>(null);
  const [customSet, setCustomSet] = useState<number[] | null>(null);
  const dayPresets = catalog.data?.workingDayPresets ?? [];
  const weekStart = week.data?.weekStartDay ?? 7;
  const savedDays = week.data?.days ?? [];
  const matchingPreset = dayPresets.find((preset) => preset.days.length === savedDays.length && preset.days.every((day) => savedDays.includes(day)));
  const dayKey = daysChoice ?? matchingPreset?.key ?? (savedDays.length > 0 ? customDays : dayPresets.find((preset) => preset.isDefault)?.key ?? customDays);
  const chosenPreset = dayPresets.find((preset) => preset.key === dayKey);
  const days = weekdaysFrom(chosenPreset?.weekStart ?? weekStart).filter((day) => (chosenPreset ? chosenPreset.days : customSet ?? savedDays).includes(day));
  const hasStoredPeriods = (shifts.data?.items ?? []).some((shift) => shift.periods.length > 0);

  function toggleDay(day: number) {
    const current = customSet ?? days;
    setCustomSet(current.includes(day) ? current.filter((item) => item !== day) : [...current, day]);
  }

  return (
    <form className="form-stack" noValidate onSubmit={(event) => event.preventDefault()}>
      <Field id="wizard-days" label={text.timing.days} hint={text.suggestion}>
        <Select id="wizard-days" value={dayKey} aria-describedby="wizard-days-hint" onChange={(event) => { setDaysChoice(event.target.value); setCustomSet(event.target.value === customDays ? days : null); }}
          options={[...dayPresets.map((preset) => ({ value: preset.key, label: preset.name })), { value: customDays, label: text.timing.customDays }]} />
      </Field>
      {dayKey === customDays && (
        <fieldset className="choice-group">
          <legend>{text.timing.customDays}</legend>
          {weekdaysFrom(weekStart).map((day) => (
            <Checkbox key={`wizard-day-${day}`} checked={days.includes(day)} onChange={() => toggleDay(day)}>{weekdayLabel(day)}</Checkbox>
          ))}
        </fieldset>
      )}
      {hasStoredPeriods && <Alert tone="info" message={text.timing.replaceNote} />}
      {week.data && <SystemForm progress={progress} days={days} weekStartDay={chosenPreset?.weekStart ?? weekStart} onBack={onBack} onDone={onDone} />}
    </form>
  );
}
