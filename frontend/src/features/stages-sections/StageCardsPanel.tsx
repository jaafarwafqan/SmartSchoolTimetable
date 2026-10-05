import { Trash2 } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { Stepper } from "../../components/ui/stepper";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { RotateCcw } from "lucide-react";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { useDailySuggestion, useSetSectionCount, useSetStageDayLessons, useStageCards, type LabelStyle, type StageCard } from "../curriculum/curriculumApi";
import { useShifts, useWorkingWeek, type Shift } from "../timetable-structure/scheduleApi";
import { weekdayLabel, weekdaysFrom } from "../timetable-structure/weekdays";

const text = messages.school.stageCards;
const maxSections = 30;
export const labelStyles: readonly LabelStyle[] = ["arabic", "numbers", "latin"];

/** Shift and label-style choices for sections added by the stepper or a template. */
export function NewSectionOptions({ idPrefix, shifts, shiftId, style, onShift, onStyle }: {
  idPrefix: string;
  shifts: Shift[];
  shiftId: number | null;
  style: LabelStyle;
  onShift: (id: number) => void;
  onStyle: (style: LabelStyle) => void;
}) {
  return (
    <div className="form-grid">
      {shifts.length > 1 && (
        <Field id={`${idPrefix}-shift`} label={text.newSectionsShift}>
          <Select id={`${idPrefix}-shift`} value={String(shiftId ?? "")} onChange={(event) => onShift(Number(event.target.value))}
            options={shifts.map((shift) => ({ value: String(shift.id), label: shift.name }))} />
        </Field>
      )}
      <Field id={`${idPrefix}-style`} label={text.labelStyle}>
        <Select id={`${idPrefix}-style`} value={style} onChange={(event) => onStyle(event.target.value as LabelStyle)}
          options={labelStyles.map((value) => ({ value, label: text.labelStyles[value] }))} />
      </Field>
    </div>
  );
}

/**
 * The stage's own lessons per day (ADR 0027): one stepper sets every working day, «تعديل لكل يوم» sets single days.
 * A count never exceeds what the stage's shift teaches that day; days equal to the shift are not stored (inherit).
 */
function StageLessons({ yearId, card, shifts, days, onSaved, onError }: {
  yearId: number; card: StageCard; shifts: Shift[]; days: number[]; onSaved: (stage: string) => void; onError: (reason: unknown) => void;
}) {
  const format = useFormatter();
  const save = useSetStageDayLessons(yearId);
  const name = card.stage.name;
  const sectionShifts = new Set(card.sections.map((section) => section.shiftId));
  const pool = shifts.filter((shift) => sectionShifts.has(shift.id));
  const relevant = pool.length > 0 ? pool : shifts;
  const shiftOn = (day: number) => Math.max(0, ...relevant.map((shift) => shift.dayLessons.find((entry) => entry.day === day)?.lessons ?? shift.lessonCount));
  const own = new Map(card.stage.dayLessons.map((entry) => [entry.day, entry.lessons]));
  const valueOn = (day: number) => Math.min(own.get(day) ?? shiftOn(day), shiftOn(day));
  const maxDaily = Math.max(0, ...days.map(shiftOn));
  const values = days.map(valueOn);
  const daily = values.length > 0 ? Math.max(...values) : 0;
  const custom = card.stage.dayLessons.length > 0;
  if (maxDaily === 0 || card.stage.isArchived) return null;

  function send(next: (day: number) => number) {
    const dayLessons = days.filter((day) => next(day) !== shiftOn(day)).map((day) => ({ day, lessons: next(day) }));
    save.mutate({ stageId: card.stage.id, dayLessons, version: card.stage.version }, { onSuccess: () => onSaved(name), onError });
  }

  return (
    <div className="stage-lessons">
      <div className="stage-lessons-head">
        <span className="stepper-caption" aria-hidden="true">{text.dailyLessons}</span>
        <Badge tone={custom ? "primary" : "neutral"}>{custom ? text.ownCounts : text.inherits}</Badge>
      </div>
      <Stepper id={`stage-lessons-${card.stage.id}`} label={text.dailyLessonsFor(name)} value={daily} min={1} max={maxDaily} format={format.number}
        decreaseLabel={text.dailyDecrease(name)} increaseLabel={text.dailyIncrease(name)} disabled={save.isPending}
        onChange={(lessons) => send((day) => Math.min(lessons, shiftOn(day)))} />
      <details className="advanced-options">
        <summary>{text.perDay}</summary>
        <ul className="day-lessons-list">
          {days.map((day) => (
            <li key={`stage-${card.stage.id}-day-${day}`}>
              <span className="day-lessons-day">{weekdayLabel(day)}</span>
              <Stepper id={`stage-${card.stage.id}-day-${day}`} label={text.dayLessonsFor(name, weekdayLabel(day))} value={valueOn(day)} min={1} max={shiftOn(day)}
                format={format.number} decreaseLabel={text.dayDecrease(name, weekdayLabel(day))} increaseLabel={text.dayIncrease(name, weekdayLabel(day))}
                disabled={save.isPending || shiftOn(day) === 0}
                onChange={(lessons) => send((current) => (current === day ? lessons : valueOn(current)))} />
            </li>
          ))}
        </ul>
      </details>
      {custom && (
        <Button variant="ghost" size="sm" icon={<RotateCcw aria-hidden="true" size={16} />} loading={save.isPending}
          onClick={() => save.mutate({ stageId: card.stage.id, dayLessons: [], version: card.stage.version }, { onSuccess: () => onSaved(name), onError })}>
          {text.resetToShift}
        </Button>
      )}
    </div>
  );
}

/**
 * Stage cards (spec 2.5 §3.4, §6): every stage with a section stepper and its section chips. Adding names the
 * new sections automatically; removing always takes the LAST section and is confirmed first.
 */
export function StageCardsPanel({ yearId }: { yearId: number }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const cards = useStageCards(yearId);
  const shifts = useShifts(yearId);
  const week = useWorkingWeek();
  const daily = useDailySuggestion(yearId);
  const changed = new Set((daily.data?.stages ?? []).filter((stage) => stage.changedSinceSuggestion).map((stage) => stage.stageId));
  const days = weekdaysFrom(week.data?.weekStartDay ?? 7).filter((day) => (week.data?.days ?? []).includes(day));
  const setCount = useSetSectionCount(yearId);
  const [shiftChoice, setShiftChoice] = useState<number | null>(null);
  const [style, setStyle] = useState<LabelStyle>("arabic");
  const [removing, setRemoving] = useState<StageCard | null>(null);
  const shiftList = shifts.data?.items ?? [];
  const shiftId = shiftList.find((shift) => shift.id === shiftChoice)?.id ?? shiftList[0]?.id ?? null;
  const shiftName = (id: number) => shiftList.find((shift) => shift.id === id)?.name ?? "";
  const items = cards.data ?? [];

  function submit(card: StageCard, count: number) {
    feedback.reset();
    setCount.mutate({ stageId: card.stage.id, count, shiftId: shiftId ?? 0, labelStyle: style }, {
      onSuccess: (saved) => feedback.showSuccess(text.updated(saved.stage.name, format.number(saved.sections.length))),
      onError: feedback.showError,
      onSettled: () => setRemoving(null),
    });
  }

  if (items.length === 0) return null;
  return (
    <Card className="page-card" aria-labelledby="stage-cards-title">
      <h2 id="stage-cards-title">{text.title}</h2>
      <p className="card-note">{text.description}</p>
      {cards.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {shifts.isSuccess && shiftList.length === 0 && <Alert tone="warning" message={text.noShift} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {feedback.conflict && <Alert tone="error" message={messages.errors.CONFLICT} />}
      <NewSectionOptions idPrefix="cards" shifts={shiftList} shiftId={shiftId} style={style} onShift={setShiftChoice} onStyle={setStyle} />
      <ul className="stage-cards">
        {items.map((card) => {
          const capacities = [...new Map(card.sections.map((section) => [section.shiftId, section.weeklyCapacity])).entries()];
          return (
            <li key={`stage-card-${card.stage.id}`} className="stage-card">
              <div className="stage-card-header">
                <h3>{card.stage.name}</h3>
                <Stepper
                  id={`stage-card-count-${card.stage.id}`}
                  label={text.sectionCount(card.stage.name)}
                  value={card.sections.length}
                  min={0}
                  max={maxSections}
                  format={format.number}
                  decreaseLabel={text.decrease(card.stage.name)}
                  increaseLabel={text.increase(card.stage.name)}
                  disabled={setCount.isPending || (shiftId === null && card.sections.length === 0)}
                  onChange={(count) => (count < card.sections.length ? setRemoving(card) : submit(card, count))}
                />
              </div>
              {card.sections.length === 0 ? (
                <p className="stage-card-empty">{text.noSections}</p>
              ) : (
                <ul className="section-chips" aria-label={text.sectionsList(card.stage.name)}>
                  {card.sections.map((section) => (
                    <li key={`section-chip-${section.id}`} className="section-chip">
                      <bdi>{section.label}</bdi>
                      {shiftList.length > 1 && <span className="section-chip-shift">{shiftName(section.shiftId)}</span>}
                    </li>
                  ))}
                </ul>
              )}
              {changed.has(card.stage.id) && <p className="daily-changed" role="status">{messages.school.daily.changed}</p>}
              <StageLessons yearId={yearId} card={card} shifts={shiftList} days={days}
                onSaved={(stage) => feedback.showSuccess(text.lessonsSaved(stage))} onError={feedback.showError} />
              {capacities.map(([id, capacity]) => (
                <p key={`capacity-${card.stage.id}-${id}`} className="stage-card-capacity">{text.capacity(format.count(capacity, "lesson"), shiftName(id))}</p>
              ))}
            </li>
          );
        })}
      </ul>
      <ConfirmDialog
        open={removing !== null}
        danger
        title={text.removeTitle}
        consequence={removing ? text.removeConsequence(removing.sections.at(-1)?.label ?? "", removing.stage.name) : ""}
        confirmLabel={text.remove}
        confirmIcon={<Trash2 aria-hidden="true" size={20} />}
        loading={setCount.isPending}
        onCancel={() => setRemoving(null)}
        onConfirm={() => removing && submit(removing, removing.sections.length - 1)}
      />
    </Card>
  );
}
