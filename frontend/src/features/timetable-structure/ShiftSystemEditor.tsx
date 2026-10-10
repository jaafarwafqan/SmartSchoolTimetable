import { ArrowLeftRight, CalendarDays, Moon, Sun, SunMoon } from "lucide-react";
import { TimeField } from "../../components/TimeField";
import { Button } from "../../components/ui/button";
import { ToggleChipGroup } from "../../components/ui/chip-group";
import { ChoiceCards, type Choice } from "../../components/ui/choice-cards";
import { Field } from "../../components/ui/field";
import { SectionTitle } from "../../components/ui/section-title";
import { Select } from "../../components/ui/select";
import { Stepper } from "../../components/ui/stepper";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import type { PeriodPreset } from "../curriculum/curriculumApi";
import { lessonMinuteChoices, lessonsOn, maxLessonsPerDay, weeklyLessons, type ShiftPlan } from "../setup-wizard/timingPlan";
import { BreaksEditor } from "./BreaksEditor";
import { buildPeriods, reversedMorning } from "./sessionPlan";
import { applyPreset, defaultStart, noPreset, presetsFor, type ShiftSystem, type ShiftSystemValue, type TimingSession } from "./shiftSystem";
import { weekdayLabel } from "./weekdays";

const text = messages.school.shiftSystem;

const choices: readonly Choice<ShiftSystem>[] = [
  { value: "morning", label: text.systems.morning, description: text.hints.morning, icon: <Sun aria-hidden="true" size={20} /> },
  { value: "evening", label: text.systems.evening, description: text.hints.evening, icon: <Moon aria-hidden="true" size={20} /> },
  { value: "dual", label: text.systems.dual, description: text.hints.dual, icon: <SunMoon aria-hidden="true" size={20} /> },
];

type BlockProps = {
  idPrefix: string;
  session: TimingSession;
  plan: ShiftPlan;
  presets: readonly PeriodPreset[];
  days: readonly number[];
  suggestedBreak: number;
  /** The evening session of «مزدوج» shares the morning lesson count and per-day counts. */
  sharedLessons: number | null;
  onChange: (plan: ShiftPlan) => void;
};

/** The timing of one session: template (filtered to the session, «بدون قالب» first), start, lesson length, lessons, breaks and a preview. */
function TimingBlock({ idPrefix, session, plan, presets, days, suggestedBreak, sharedLessons, onChange }: BlockProps) {
  const format = useFormatter();
  const name = text.sessions[session];
  const own = presetsFor(presets, session);
  const lessons = sharedLessons ?? plan.lessonCount;
  const rows = buildPeriods({ firstStart: plan.firstStartTime, lessonMinutes: plan.lessonMinutes, breaks: plan.breaks, gapMinutes: plan.gapMinutes }, lessons);
  const Icon = session === "morning" ? Sun : Moon;
  return (
    <section className="shift-system-block" aria-labelledby={`${idPrefix}-title`}>
      <SectionTitle level={3} icon={Icon} id={`${idPrefix}-title`}>{text.timingOf(name)}</SectionTitle>
      <div className="form-grid">
        <Field id={`${idPrefix}-template`} label={text.template} hint={text.templateHint}>
          <Select id={`${idPrefix}-template`} aria-describedby={`${idPrefix}-template-hint`} value={plan.presetKey}
            onChange={(event) => onChange(applyPreset(plan, own, event.target.value, session))}
            options={[{ value: noPreset, label: text.noTemplate }, ...own.map((item) => ({ value: item.key, label: item.name }))]} />
        </Field>
        <TimeField id={`${idPrefix}-start`} label={text.firstStart(name)} value={plan.firstStartTime} onChange={(time) => onChange({ ...plan, firstStartTime: time })} />
        <Field id={`${idPrefix}-minutes`} label={text.lessonMinutes(name)}>
          <Select id={`${idPrefix}-minutes`} value={String(plan.lessonMinutes)} onChange={(event) => onChange({ ...plan, lessonMinutes: Number(event.target.value) })}
            options={[...new Set([...lessonMinuteChoices, plan.lessonMinutes])].sort((a, b) => a - b).map((minutes) => ({ value: String(minutes), label: format.count(minutes, "minute") }))} />
        </Field>
        {sharedLessons === null
          ? (
            <div className="ui-field">
              <span className="stepper-caption" aria-hidden="true">{text.lessons}</span>
              <Stepper id={`${idPrefix}-lessons`} label={text.lessons} value={plan.lessonCount} min={1} max={maxLessonsPerDay} format={format.number}
                decreaseLabel={text.lessonsDecrease} increaseLabel={text.lessonsIncrease} onChange={(lessonCount) => onChange({ ...plan, lessonCount })} />
            </div>
          )
          : <p className="sessions-count">{text.sharedLessons(format.count(sharedLessons, "lesson"))}</p>}
      </div>
      <BreaksEditor idPrefix={idPrefix} lessonCount={lessons} breaks={plan.breaks} gapMinutes={plan.gapMinutes}
        firstStartTime={plan.firstStartTime} lessonMinutes={plan.lessonMinutes}
        suggestedMinutes={suggestedBreak} onChange={(breaks, gapMinutes) => onChange({ ...plan, breaks, gapMinutes })} />
      {sharedLessons === null && (
        <details className="advanced-options">
          <summary>{text.perDay}</summary>
          <ul className="day-lessons-list form-stack">
            {days.map((day) => (
              <li key={`${idPrefix}-day-${day}`}>
                <span className="day-lessons-day">{weekdayLabel(day)}</span>
                <Stepper id={`${idPrefix}-day-${day}`} label={messages.school.scheduleStructure.dayLessonsFor(weekdayLabel(day))} value={lessonsOn(plan, day)} min={0} max={plan.lessonCount}
                  format={format.number} decreaseLabel={messages.school.scheduleStructure.decreaseFor(weekdayLabel(day))} increaseLabel={messages.school.scheduleStructure.increaseFor(weekdayLabel(day))}
                  onChange={(count) => onChange({ ...plan, dayLessons: { ...plan.dayLessons, [day]: count } })} />
              </li>
            ))}
          </ul>
        </details>
      )}
      {sharedLessons === null && <p className="card-note">{text.weekly(format.number(weeklyLessons(plan, days)))}</p>}
      {rows.length > 0 && (
        <ol className="sessions-preview" aria-label={text.preview(name)}>
          {rows.map((row, index) => {
            const number = rows.slice(0, index + 1).filter((item) => item.kind === "lesson").length;
            return (
              <li key={`${row.kind}-${row.startTime}`} className={row.kind === "break" ? "is-break" : undefined}>
                {row.kind === "lesson"
                  ? text.previewRow(format.number(number), format.time(row.startTime), format.time(row.endTime))
                  : text.previewBreak(format.time(row.startTime), format.time(row.endTime))}
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}

/** Changing between morning and evening moves an untouched default start to the other session's default. */
function withSystem(value: ShiftSystemValue, system: ShiftSystem): ShiftSystemValue {
  const from: TimingSession = value.system === "evening" ? "evening" : "morning";
  const to: TimingSession = system === "evening" ? "evening" : "morning";
  const main = from !== to && value.main.firstStartTime === defaultStart[from] ? { ...value.main, firstStartTime: defaultStart[to] } : value.main;
  return { ...value, system, main };
}

type Props = {
  idPrefix: string;
  value: ShiftSystemValue;
  days: readonly number[];
  presets: readonly PeriodPreset[];
  suggestedBreak: number;
  onChange: (value: ShiftSystemValue) => void;
};

/**
 * «نظام الدوام» (MF7), the ONE control everywhere: صباحي / مسائي / مزدوج. A single system shows one timing; «مزدوج» shows the
 * morning and evening timings side by side (same lesson count), then the morning days of each semester with «اعكس للفصل الثاني».
 */
export function ShiftSystemEditor({ idPrefix, value, days, presets, suggestedBreak, onChange }: Props) {
  const mainSession: TimingSession = value.system === "evening" ? "evening" : "morning";
  const dayOptions = days.map((day) => ({ value: day, label: weekdayLabel(day) }));
  const eveningList = (term: 1 | 2) => days.filter((day) => !value.morningDays[term].includes(day)).map(weekdayLabel);
  const toggle = (term: 1 | 2, day: number) => onChange({
    ...value,
    morningDays: { ...value.morningDays, [term]: value.morningDays[term].includes(day) ? value.morningDays[term].filter((item) => item !== day) : [...value.morningDays[term], day] },
  });
  return (
    <div className="shift-system">
      <ChoiceCards name={`${idPrefix}-system`} legend={text.legend} choices={choices} value={value.system} onChange={(system) => onChange(withSystem(value, system))} />
      <div className={value.system === "dual" ? "shift-system-sessions is-dual" : "shift-system-sessions"}>
        <TimingBlock idPrefix={`${idPrefix}-${mainSession}`} session={mainSession} plan={value.main} presets={presets} days={days} suggestedBreak={suggestedBreak}
          sharedLessons={null} onChange={(main) => onChange({ ...value, main })} />
        {value.system === "dual" && (
          <TimingBlock idPrefix={`${idPrefix}-evening`} session="evening" plan={value.evening} presets={presets} days={days} suggestedBreak={suggestedBreak}
            sharedLessons={value.main.lessonCount} onChange={(evening) => onChange({ ...value, evening })} />
        )}
      </div>
      {value.system === "dual" && (
        <section className="shift-system-block" aria-labelledby={`${idPrefix}-mapping-title`}>
          <SectionTitle level={3} icon={CalendarDays} id={`${idPrefix}-mapping-title`}>{text.mapping}</SectionTitle>
          <div className="shift-system-terms">
            {([1, 2] as const).map((term) => (
              <div key={term} className="sessions-term">
                <SectionTitle level={3} icon={term === 1 ? Sun : Moon}>{term === 1 ? text.term1 : text.term2}</SectionTitle>
                <p className="ui-field-label">{text.chooseMorning}</p>
                <ToggleChipGroup label={`${term === 1 ? text.term1 : text.term2}: ${text.chooseMorning}`} options={dayOptions} values={value.morningDays[term]}
                  onToggle={(day) => toggle(term, day)} />
                <p className="card-note" aria-live="polite">
                  {eveningList(term).length > 0 ? text.eveningDays(eveningList(term).join(text.daysSeparator)) : text.noEveningDays}
                </p>
                {term === 1 && (
                  <Button variant="secondary" size="sm" icon={<ArrowLeftRight aria-hidden="true" size={16} />} title={text.reverseHint}
                    onClick={() => onChange({ ...value, morningDays: { ...value.morningDays, 2: reversedMorning(value.morningDays[1], days) } })}>
                    {text.reverse}
                  </Button>
                )}
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
