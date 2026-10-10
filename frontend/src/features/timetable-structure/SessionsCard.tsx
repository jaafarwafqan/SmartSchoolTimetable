import { ArrowLeftRight, Check, Clock3, Moon, Sun, SunMoon } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { TimeField } from "../../components/TimeField";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ToggleChipGroup } from "../../components/ui/chip-group";
import { ChoiceCards, type Choice } from "../../components/ui/choice-cards";
import { Stepper } from "../../components/ui/stepper";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useTemplateCatalog } from "../curriculum/curriculumApi";
import { useSchoolProfile } from "../school-profile/profileApi";
import { breakIssues, BreaksEditor } from "./BreaksEditor";
import {
  buildPeriods, mappingOf, maxLessonMinutes, minLessonMinutes, morningDays, reversedMorning, timingForm,
  type SessionPlan, type TimingForm,
} from "./sessionPlan";
import { useSaveSessionPlan, useSessionPlan } from "./sessionPlanApi";
import { weekdayLabel } from "./weekdays";

const text = messages.school.sessions;
type Mode = "oneSession" | "twoSessions";
const choices: readonly Choice<Mode>[] = [
  { value: "oneSession", label: text.oneSession, description: text.oneSessionHint, icon: <Sun size={20} /> },
  { value: "twoSessions", label: text.twoSessions, description: text.twoSessionsHint, icon: <SunMoon size={20} /> },
];

function MorningSummary({ plan }: { plan: SessionPlan }) {
  const format = useFormatter();
  const rows = plan.timings.find((timing) => timing.session === "morning")?.periods ?? [];
  const lessons = rows.filter((row) => row.kind === "lesson");
  return (
    <section className="sessions-block" aria-labelledby="sessions-morning-title">
      <h3 id="sessions-morning-title"><Sun aria-hidden="true" size={18} /><span>{text.morningTiming}</span></h3>
      {lessons.length > 0
        ? <p>{text.morningSummary(format.time(lessons[0].startTime), format.time(lessons[lessons.length - 1].endTime), format.count(lessons.length, "lesson"))}</p>
        : <Alert tone="warning" message={text.noMorningPeriods} />}
      <p className="card-note">{text.morningFromShift}</p>
    </section>
  );
}

/** The editor once the plan is loaded (the form starts from the saved values). */
function SessionsForm({ plan, feedback }: { plan: SessionPlan; feedback: ReturnType<typeof useFormFeedback> }) {
  const format = useFormatter();
  const save = useSaveSessionPlan();
  const catalog = useTemplateCatalog().data;
  const schoolType = useSchoolProfile().data?.schoolType ?? "other";
  const suggestedBreak = catalog?.breakDefaults.minutes[schoolType] ?? 15;
  const morningRows = plan.timings.find((timing) => timing.session === "morning")?.periods ?? [];
  const morningForm = timingForm(morningRows, "08:00", 40);
  const [mode, setMode] = useState<Mode>(plan.system === "twoSessions" ? "twoSessions" : "oneSession");
  const [evening, setEvening] = useState<TimingForm>(() =>
    timingForm(plan.timings.find((timing) => timing.session === "evening")?.periods, "13:00", morningForm.lessonMinutes));
  const [morning, setMorning] = useState<Record<1 | 2, number[]>>(() => {
    const first = plan.days.length > 0 ? morningDays(plan.days, 1, plan.workingDays) : plan.workingDays.slice(0, Math.ceil(plan.workingDays.length / 2));
    return { 1: first, 2: plan.days.length > 0 ? morningDays(plan.days, 2, plan.workingDays) : reversedMorning(first, plan.workingDays) };
  });
  const lessons = plan.lessonCount;
  const rows = buildPeriods(evening, lessons);
  const issues = breakIssues(lessons, evening.breaks).some((issue) => issue !== null);
  const changed = mode !== plan.system || mode === "twoSessions";

  function toggle(term: 1 | 2, day: number) {
    feedback.reset();
    setMorning((current) => ({ ...current, [term]: current[term].includes(day) ? current[term].filter((item) => item !== day) : [...current[term], day] }));
  }

  function submit() {
    feedback.reset();
    if (mode === "twoSessions" && issues) {
      feedback.setError(text.blocked);
      return;
    }
    save.mutate({
      system: mode,
      timings: mode === "twoSessions" ? [{ session: "evening", periods: rows }] : [],
      days: mode === "twoSessions" ? mappingOf(morning, plan.workingDays) : [],
      version: plan.version,
    }, { onSuccess: () => feedback.showSuccess(text.saved), onError: feedback.showError });
  }

  const dayOptions = plan.workingDays.map((day) => ({ value: day, label: weekdayLabel(day) }));
  const eveningList = (term: 1 | 2) => plan.workingDays.filter((day) => !morning[term].includes(day)).map(weekdayLabel);

  return (
    <>
      <ChoiceCards name="session-system" legend={text.legend} choices={choices} value={mode} onChange={(value) => { feedback.reset(); setMode(value); }} />
      <p className="card-note sessions-soon"><Clock3 aria-hidden="true" size={16} /><span>{text.threeSessionsSoon}</span></p>
      {mode === "twoSessions" && (
        <div className="sessions-editor">
          <MorningSummary plan={plan} />
          <section className="sessions-block" aria-labelledby="sessions-evening-title">
            <h3 id="sessions-evening-title"><Moon aria-hidden="true" size={18} /><span>{text.eveningTiming}</span></h3>
            <p className="card-note">{text.eveningHint}</p>
            <div className="form-grid">
              <TimeField id="evening-first-start" label={text.firstStart} value={evening.firstStart} required
                onChange={(value) => setEvening((current) => ({ ...current, firstStart: value }))} />
              <div className="ui-field">
                <span className="stepper-caption" aria-hidden="true">{text.lessonMinutes}</span>
                <Stepper id="evening-lesson-minutes" label={text.lessonMinutes} value={evening.lessonMinutes} min={minLessonMinutes} max={maxLessonMinutes}
                  format={(minutes) => format.count(minutes, "minute")} decreaseLabel={text.shorterLesson} increaseLabel={text.longerLesson}
                  onChange={(minutes) => setEvening((current) => ({ ...current, lessonMinutes: minutes }))} />
              </div>
              <p className="sessions-count">{text.lessonCount(format.count(lessons, "lesson"))}</p>
            </div>
            <BreaksEditor idPrefix="evening" lessonCount={lessons} breaks={evening.breaks} gapMinutes={evening.gapMinutes} suggestedMinutes={suggestedBreak}
              firstStartTime={evening.firstStart} lessonMinutes={evening.lessonMinutes}
              onChange={(breaks, gapMinutes) => setEvening((current) => ({ ...current, breaks, gapMinutes }))} />
            {rows.length > 0 && (
              <ol className="sessions-preview" aria-label={text.preview}>
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
          <section className="sessions-block" aria-labelledby="sessions-mapping-title">
            <h3 id="sessions-mapping-title"><SunMoon aria-hidden="true" size={18} /><span>{text.mapping}</span></h3>
            {([1, 2] as const).map((term) => (
              <div key={term} className="sessions-term">
                <h4>{term === 1 ? text.term1 : text.term2}</h4>
                <p className="ui-field-label">{text.chooseMorning}</p>
                <ToggleChipGroup label={`${term === 1 ? text.term1 : text.term2}: ${text.chooseMorning}`} options={dayOptions} values={morning[term]} onToggle={(day) => toggle(term, day)} />
                <p className="card-note" aria-live="polite">
                  {eveningList(term).length > 0 ? text.eveningDays(eveningList(term).join(text.daysSeparator)) : text.noEveningDays}
                </p>
                {term === 1 && (
                  <Button variant="secondary" size="sm" icon={<ArrowLeftRight aria-hidden="true" size={16} />} title={text.reverseHint}
                    onClick={() => { feedback.reset(); setMorning((current) => ({ ...current, 2: reversedMorning(current[1], plan.workingDays) })); }}>
                    {text.reverse}
                  </Button>
                )}
              </div>
            ))}
          </section>
        </div>
      )}
      <div className="form-actions">
        <Button icon={<Check aria-hidden="true" size={20} />} loading={save.isPending} disabled={!changed || feedback.conflict} onClick={submit}>{text.save}</Button>
      </div>
    </>
  );
}

/**
 * «نظام الدوام اليومي» (R3): one session (default, nothing changes) or a double session (دوام مزدوج) with the same
 * sections: the evening timing (same lesson count; start, length and breaks chosen from controls) and, per semester,
 * which working days are morning (the others are evening), with one-click «اعكس للفصل الثاني».
 */
export function SessionsCard() {
  const plan = useSessionPlan();
  // Kept here: a save changes the plan's version, which starts a fresh form with the saved values.
  const feedback = useFormFeedback();
  return (
    <Card className="page-card" aria-labelledby="sessions-title">
      <h2 id="sessions-title">{text.title}</h2>
      <p className="ui-field-hint">{text.description}</p>
      {plan.isError && <Alert tone="error" message={userErrorMessage(plan.error)} />}
      {plan.data && !plan.data.available && <Alert tone="info" message={text.unavailable} />}
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); void plan.refetch(); }} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {plan.data?.available && <SessionsForm key={plan.data.version} plan={plan.data} feedback={feedback} />}
    </Card>
  );
}
