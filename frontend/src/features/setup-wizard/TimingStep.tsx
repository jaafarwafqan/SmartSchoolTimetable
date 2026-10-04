import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { apiRequest } from "../../api";
import { TimeField } from "../../components/TimeField";
import { Alert } from "../../components/ui/alert";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { Stepper } from "../../components/ui/stepper";
import { DataTable } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useTemplateCatalog, type PeriodPreset } from "../curriculum/curriculumApi";
import { useShifts, useWorkingWeek, type Period } from "../timetable-structure/scheduleApi";
import { weekdayLabel, weekdaysFrom } from "../timetable-structure/weekdays";
import { lessonMinuteChoices, lessonsOn, maxLessonsPerDay, planFromPreset, planFromShift, toShiftInput, weeklyLessons, type ShiftPlan } from "./timingPlan";
import { WizardFooter } from "./WizardFrame";
import { useSaveTimingStep, type SetupProgress } from "./wizardApi";

const text = messages.school.wizard;
const customDays = "custom";
type Kind = "morning" | "evening";
const defaultStart: Record<Kind, string> = { morning: "08:00", evening: "13:00" };
const kindsFor = (mode: SetupProgress["shiftMode"]): Kind[] => (mode === "dual" ? ["morning", "evening"] : [mode]);

/** Live preview of the periods a plan generates (server generator, nothing saved). */
function PeriodsPreview({ yearId, kind, plan }: { yearId: number; kind: Kind; plan: ShiftPlan }) {
  const format = useFormatter();
  const body = { firstStartTime: plan.firstStartTime, lessonMinutes: plan.lessonMinutes, lessonCount: plan.lessonCount, breakMinutes: 0, breakAfterLesson: null,
    breaks: plan.breaks.filter((slot) => slot.afterLesson < plan.lessonCount) };
  const preview = useQuery({
    queryKey: ["wizard-periods", yearId, body],
    queryFn: () => apiRequest<{ periods: Period[] }>(`/api/v1/academic-years/${yearId}/shifts/generate-periods`, "POST", body),
    retry: false,
  });
  const rows = preview.data?.periods ?? [];
  let lesson = 0;
  const numbered = rows.map((period) => ({ ...period, lesson: period.kind === "lesson" ? ++lesson : null }));
  return (
    <DataTable caption={text.timing.previewTitle(text.timing.shifts[kind])} rows={numbered} rowKey={(row) => `${kind}-${row.position}`} loading={preview.isPending}
      columns={[
        { key: "period", header: text.timing.period, cell: (row) => (row.lesson === null ? text.timing.breakRow : text.timing.lessonRow(format.number(row.lesson))) },
        { key: "from", header: text.timing.from, cell: (row) => format.time(row.startTime), numeric: true },
        { key: "to", header: text.timing.to, cell: (row) => format.time(row.endTime), numeric: true },
      ]} />
  );
}

function ShiftBlock({ kind, plan, days, presets, yearId, onChange }: {
  kind: Kind; plan: ShiftPlan; days: number[]; presets: PeriodPreset[]; yearId: number | null; onChange: (plan: ShiftPlan) => void;
}) {
  const format = useFormatter();
  const name = text.timing.shifts[kind];
  return (
    <section className="wizard-shift" aria-labelledby={`wizard-shift-${kind}`}>
      <h3 id={`wizard-shift-${kind}`}>{name}</h3>
      <div className="form-grid">
        <Field id={`wizard-${kind}-preset`} label={text.timing.breakPattern}>
          <Select id={`wizard-${kind}-preset`} value={plan.presetKey}
            onChange={(event) => onChange({ ...planFromPreset(presets.find((item) => item.key === event.target.value), plan.firstStartTime), dayLessons: plan.dayLessons })}
            options={[...(plan.presetKey ? [] : [{ value: "", label: messages.school.templates.presetNone }]), ...presets.map((item) => ({ value: item.key, label: item.name }))]} />
        </Field>
        <TimeField id={`wizard-${kind}-start`} label={text.timing.firstStart(name)} value={plan.firstStartTime} onChange={(time) => onChange({ ...plan, firstStartTime: time })} />
        <Field id={`wizard-${kind}-minutes`} label={text.timing.minutes(name)}>
          <Select id={`wizard-${kind}-minutes`} value={String(plan.lessonMinutes)} onChange={(event) => onChange({ ...plan, lessonMinutes: Number(event.target.value) })}
            options={[...new Set([...lessonMinuteChoices, plan.lessonMinutes])].sort((a, b) => a - b).map((minutes) => ({ value: String(minutes), label: text.timing.minutesOption(format.number(minutes)) }))} />
        </Field>
        <div className="ui-field">
          <span className="stepper-caption" aria-hidden="true">{text.timing.lessons(name)}</span>
          <Stepper id={`wizard-${kind}-lessons`} label={text.timing.lessons(name)} value={plan.lessonCount} min={1} max={maxLessonsPerDay} format={format.number}
            decreaseLabel={text.timing.lessonsDecrease(name)} increaseLabel={text.timing.lessonsIncrease(name)} onChange={(lessonCount) => onChange({ ...plan, lessonCount })} />
        </div>
      </div>
      <details className="advanced-options">
        <summary>{text.timing.perDay}</summary>
        <ul className="day-lessons-list form-stack">
          {days.map((day) => (
            <li key={`${kind}-day-${day}`}>
              <span className="day-lessons-day">{weekdayLabel(day)}</span>
              <Stepper id={`wizard-${kind}-day-${day}`} label={messages.school.scheduleStructure.dayLessonsFor(weekdayLabel(day))} value={lessonsOn(plan, day)} min={0} max={plan.lessonCount}
                format={format.number} decreaseLabel={messages.school.scheduleStructure.decreaseFor(weekdayLabel(day))} increaseLabel={messages.school.scheduleStructure.increaseFor(weekdayLabel(day))}
                onChange={(lessons) => onChange({ ...plan, dayLessons: { ...plan.dayLessons, [day]: lessons } })} />
            </li>
          ))}
        </ul>
      </details>
      <p className="card-note">{text.timing.weekly(format.number(weeklyLessons(plan, days)))}</p>
      {yearId !== null && <PeriodsPreview yearId={yearId} kind={kind} plan={plan} />}
    </section>
  );
}

/** Step 3: working days, then one block per shift of the chosen mode with a live period preview. */
export function TimingStep({ progress, onBack, onDone }: { progress: SetupProgress; onBack: () => void; onDone: () => void }) {
  const feedback = useFormFeedback();
  const save = useSaveTimingStep();
  const catalog = useTemplateCatalog();
  const context = useSchoolContext();
  const week = useWorkingWeek();
  const yearId = context.data?.currentYear?.id ?? null;
  const shifts = useShifts(yearId);
  const [daysChoice, setDaysChoice] = useState<string | null>(null);
  const [customSet, setCustomSet] = useState<number[] | null>(null);
  const [plans, setPlans] = useState<Partial<Record<Kind, ShiftPlan>>>({});
  const presets = catalog.data?.periodPresets ?? [];
  const dayPresets = catalog.data?.workingDayPresets ?? [];
  const weekStart = week.data?.weekStartDay ?? 7;
  const savedDays = week.data?.days ?? [];
  const matchingPreset = dayPresets.find((preset) => preset.days.length === savedDays.length && preset.days.every((day) => savedDays.includes(day)));
  const dayKey = daysChoice ?? matchingPreset?.key ?? (savedDays.length > 0 ? customDays : dayPresets.find((preset) => preset.isDefault)?.key ?? customDays);
  const chosenPreset = dayPresets.find((preset) => preset.key === dayKey);
  const days = weekdaysFrom(chosenPreset?.weekStart ?? weekStart).filter((day) => (chosenPreset ? chosenPreset.days : customSet ?? savedDays).includes(day));
  const kinds = kindsFor(progress.shiftMode);
  const planFor = (kind: Kind): ShiftPlan => {
    const stored = shifts.data?.items.find((shift) => shift.kind === kind);
    return plans[kind] ?? (stored && planFromShift(stored)) ?? planFromPreset(presets[0], presets[0] && kind === "morning" ? presets[0].firstStart : defaultStart[kind]);
  };
  const hasStoredPeriods = (shifts.data?.items ?? []).some((shift) => shift.periods.length > 0);

  function toggleDay(day: number) {
    const current = customSet ?? days;
    setCustomSet(current.includes(day) ? current.filter((item) => item !== day) : [...current, day]);
  }

  return (
    <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={(event) => event.preventDefault()}>
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
      {kinds.map((kind) => (
        <ShiftBlock key={`wizard-shift-${kind}`} kind={kind} plan={planFor(kind)} days={days} presets={presets} yearId={yearId}
          onChange={(plan) => setPlans((current) => ({ ...current, [kind]: plan }))} />
      ))}
      <WizardFooter step={3} pending={save.isPending} error={feedback.error} onBack={onBack}
        onNext={() => {
          feedback.reset();
          save.mutate(
            { days, weekStartDay: chosenPreset?.weekStart ?? weekStart, shifts: kinds.map((kind) => toShiftInput(kind, planFor(kind), days)) },
            { onSuccess: onDone, onError: feedback.showError },
          );
        }} />
    </form>
  );
}
