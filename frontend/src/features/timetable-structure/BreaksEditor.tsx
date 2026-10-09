import { CircleAlert, Plus, Trash2 } from "lucide-react";
import { Button } from "../../components/ui/button";
import { ChipGroup } from "../../components/ui/chip-group";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { Stepper } from "../../components/ui/stepper";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { clockOf } from "../../lib/time";

const text = messages.school.scheduleStructure.breaks;
export type BreakSlot = { afterLesson: number; minutes: number };
/** R2: any duration 1–60 minutes in the editor (the server accepts 1–120); quick picks for the usual lengths. */
export const minBreakMinutes = 1;
export const maxBreakMinutes = 60;
export const breakQuickPicks = [5, 10, 15, 20, 30] as const;
export const gapMinuteChoices = [0, 5, 10] as const;

export type BreakIssue = "duplicate" | "afterLast";

type BreaksEditorProps = {
  idPrefix: string;
  lessonCount: number;
  breaks: readonly BreakSlot[];
  gapMinutes: number;
  /** Suggested length for a new break (templates, per school type; a suggestion only). */
  suggestedMinutes: number;
  /** First lesson start ("HH:mm") and lesson length: when given, each break shows its clock time. */
  firstStartTime?: string;
  lessonMinutes?: number;
  onChange: (breaks: BreakSlot[], gapMinutes: number) => void;
};

/** The first lesson without a break after it (or null when every gap already has one). */
export function freePosition(lessonCount: number, breaks: readonly BreakSlot[]): number | null {
  for (let lesson = Math.max(1, Math.min(3, lessonCount - 1)); lesson < lessonCount; lesson++) if (!breaks.some((slot) => slot.afterLesson === lesson)) return lesson;
  for (let lesson = 1; lesson < lessonCount; lesson++) if (!breaks.some((slot) => slot.afterLesson === lesson)) return lesson;
  return null;
}

/** Per break: a second break in the same gap, or a break after the last lesson (the server refuses both). */
export function breakIssues(lessonCount: number, breaks: readonly BreakSlot[]): (BreakIssue | null)[] {
  return breaks.map((slot, index) => {
    if (slot.afterLesson >= lessonCount || slot.afterLesson < 1) return "afterLast";
    return breaks.findIndex((other) => other.afterLesson === slot.afterLesson) !== index ? "duplicate" : null;
  });
}

/** Breaks the generator can use now (the ones without an issue); the editor keeps showing the others with a message. */
export const validBreaks = (lessonCount: number, breaks: readonly BreakSlot[]) => {
  const issues = breakIssues(lessonCount, breaks);
  return breaks.filter((_, index) => issues[index] === null);
};

/** Clock time of a break: the first start, the lessons and breaks before it, and the gaps between lessons without a break. */
export function breakClock(firstStartTime: string, lessonMinutes: number, gapMinutes: number, breaks: readonly BreakSlot[], slot: BreakSlot): { start: string; end: string } | null {
  const match = /^(\d{2}):(\d{2})$/.exec(firstStartTime);
  if (!match) return null;
  const before = breaks.filter((other) => other.afterLesson < slot.afterLesson);
  const gaps = slot.afterLesson - 1 - before.length;
  const start = Number(match[1]) * 60 + Number(match[2]) + slot.afterLesson * lessonMinutes + before.reduce((sum, other) => sum + other.minutes, 0) + gaps * gapMinutes;
  return { start: clockOf(start), end: clockOf(start + slot.minutes) };
}

/**
 * Breaks of one shift (ADR 0026, R2): a break after any lesson 1..N−1, one per gap, as many as the owner wants, or
 * none. Each row is edited in place: its position, its duration (a 1–60 minute stepper and quick picks), its clock
 * time and a delete action. Shared by wizard step 3 and the periods generator.
 */
export function BreaksEditor({ idPrefix, lessonCount, breaks, gapMinutes, suggestedMinutes, firstStartTime, lessonMinutes, onChange }: BreaksEditorProps) {
  const format = useFormatter();
  const sorted = [...breaks].sort((a, b) => a.afterLesson - b.afterLesson);
  const issues = breakIssues(lessonCount, sorted);
  const position = freePosition(lessonCount, sorted);
  const update = (index: number, slot: BreakSlot) => onChange(sorted.map((item, current) => (current === index ? slot : item)), gapMinutes);
  const valid = validBreaks(lessonCount, sorted);
  const clamp = (minutes: number) => Math.max(minBreakMinutes, Math.min(maxBreakMinutes, minutes));

  return (
    <fieldset className="breaks-editor">
      <legend>{text.legend} <span className="breaks-suggested">{text.suggested(format.count(suggestedMinutes, "minute"))}</span></legend>
      {sorted.length === 0 && <p className="card-note">{text.none}</p>}
      <ul className="breaks-list">
        {sorted.map((slot, index) => {
          const number = format.number(index + 1);
          const taken = new Set(sorted.filter((_, other) => other !== index).map((item) => item.afterLesson));
          const issue = issues[index];
          const clock = issue === null && firstStartTime && lessonMinutes ? breakClock(firstStartTime, lessonMinutes, gapMinutes, valid, slot) : null;
          const positions = Array.from({ length: Math.max(0, lessonCount - 1) }, (_, lesson) => lesson + 1).filter((lesson) => !taken.has(lesson));
          return (
            <li key={`${idPrefix}-break-${index}`} className={`breaks-row${issue ? " has-issue" : ""}`}>
              <Field id={`${idPrefix}-break-${index}-after`} label={text.after(number)}>
                <Select id={`${idPrefix}-break-${index}-after`} value={String(slot.afterLesson)} aria-invalid={issue ? true : undefined}
                  aria-describedby={issue ? `${idPrefix}-break-${index}-issue` : undefined}
                  onChange={(event) => update(index, { ...slot, afterLesson: Number(event.target.value) })}
                  options={[...(positions.includes(slot.afterLesson) ? [] : [{ value: String(slot.afterLesson), label: text.lesson(format.number(slot.afterLesson)) }]),
                    ...positions.map((lesson) => ({ value: String(lesson), label: text.lesson(format.number(lesson)) }))]} />
              </Field>
              <div className="ui-field breaks-duration">
                <span className="stepper-caption" aria-hidden="true">{text.duration(number)}</span>
                <Stepper id={`${idPrefix}-break-${index}-minutes`} label={text.duration(number)} value={slot.minutes} min={minBreakMinutes} max={maxBreakMinutes}
                  format={(minutes) => format.count(minutes, "minute")} decreaseLabel={text.shorter(number)} increaseLabel={text.longer(number)}
                  onChange={(minutes) => update(index, { ...slot, minutes: clamp(minutes) })} />
              </div>
              <Button variant="ghost" size="sm" className="breaks-remove" icon={<Trash2 aria-hidden="true" size={16} />} aria-label={text.remove(number)} title={text.remove(number)}
                onClick={() => onChange(sorted.filter((_, current) => current !== index), gapMinutes)}>{text.removeLabel}</Button>
              <div className="breaks-row-actions">
                <ChipGroup label={text.quickPicks(number)} caption={text.minutesCaption} value={slot.minutes}
                  options={breakQuickPicks.map((minutes) => ({ value: minutes, label: format.number(minutes), name: format.count(minutes, "minute") }))}
                  onChange={(minutes) => update(index, { ...slot, minutes })} />
                {clock && <p className="breaks-clock">{text.clock(format.time(clock.start), format.time(clock.end))}</p>}
              </div>
              {issue && (
                <p id={`${idPrefix}-break-${index}-issue`} className="ui-field-error">
                  <CircleAlert aria-hidden="true" size={16} strokeWidth={2} />
                  <span>{issue === "afterLast" ? text.afterLast : text.duplicate}</span>
                </p>
              )}
            </li>
          );
        })}
      </ul>
      <div className="breaks-actions">
        <Field id={`${idPrefix}-gap`} label={text.gap}>
          <Select id={`${idPrefix}-gap`} value={String(gapMinutes)} onChange={(event) => onChange(sorted, Number(event.target.value))}
            options={gapMinuteChoices.map((minutes) => ({ value: String(minutes), label: minutes === 0 ? text.noGap : format.count(minutes, "minute") }))} />
        </Field>
        <Button variant="secondary" size="sm" icon={<Plus aria-hidden="true" size={16} />} disabled={position === null}
          onClick={() => position !== null && onChange([...sorted, { afterLesson: position, minutes: clamp(suggestedMinutes) }], gapMinutes)}>{text.add}</Button>
        {position === null && lessonCount > 1 && <span className="card-note">{text.allGapsUsed}</span>}
      </div>
    </fieldset>
  );
}
