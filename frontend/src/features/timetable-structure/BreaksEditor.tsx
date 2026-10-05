import { Plus, Trash2 } from "lucide-react";
import { Button } from "../../components/ui/button";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";

const text = messages.school.scheduleStructure.breaks;
export type BreakSlot = { afterLesson: number; minutes: number };
export const maxBreaks = 3;
export const breakMinuteChoices = [5, 10, 15, 20, 25, 30, 40, 45, 60] as const;
export const gapMinuteChoices = [0, 5, 10] as const;

type BreaksEditorProps = {
  idPrefix: string;
  lessonCount: number;
  breaks: readonly BreakSlot[];
  gapMinutes: number;
  /** Suggested length for a new break (templates, per school type; a suggestion only). */
  suggestedMinutes: number;
  onChange: (breaks: BreakSlot[], gapMinutes: number) => void;
};

/** The first lesson without a break after it (or null when every gap already has one). */
export function freePosition(lessonCount: number, breaks: readonly BreakSlot[]): number | null {
  for (let lesson = Math.max(1, Math.min(3, lessonCount - 1)); lesson < lessonCount; lesson++) if (!breaks.some((slot) => slot.afterLesson === lesson)) return lesson;
  for (let lesson = 1; lesson < lessonCount; lesson++) if (!breaks.some((slot) => slot.afterLesson === lesson)) return lesson;
  return null;
}

/**
 * Breaks of one shift (ADR 0026): up to three, each with its position (after lesson N) and its own duration, plus an
 * optional gap between lessons. Presets only fill these values; everything stays editable. Shared by wizard step 3
 * and the periods generator.
 */
export function BreaksEditor({ idPrefix, lessonCount, breaks, gapMinutes, suggestedMinutes, onChange }: BreaksEditorProps) {
  const format = useFormatter();
  const sorted = [...breaks].sort((a, b) => a.afterLesson - b.afterLesson);
  const position = freePosition(lessonCount, sorted);
  const update = (index: number, slot: BreakSlot) => onChange(sorted.map((item, current) => (current === index ? slot : item)), gapMinutes);
  const minuteOptions = (value: number) => [...new Set([...breakMinuteChoices, value])].sort((a, b) => a - b)
    .map((minutes) => ({ value: String(minutes), label: format.count(minutes, "minute") }));

  return (
    <fieldset className="breaks-editor">
      <legend>{text.legend}</legend>
      <p className="card-note">{text.suggested(format.count(suggestedMinutes, "minute"))}</p>
      {sorted.length === 0 && <p className="card-note">{text.none}</p>}
      <ul className="breaks-list">
        {sorted.map((slot, index) => {
          const number = format.number(index + 1);
          const taken = new Set(sorted.filter((_, other) => other !== index).map((item) => item.afterLesson));
          return (
            <li key={`${idPrefix}-break-${slot.afterLesson}`} className="breaks-row">
              <Field id={`${idPrefix}-break-${index}-after`} label={text.after(number)}>
                <Select id={`${idPrefix}-break-${index}-after`} value={String(slot.afterLesson)}
                  onChange={(event) => update(index, { ...slot, afterLesson: Number(event.target.value) })}
                  options={Array.from({ length: Math.max(0, lessonCount - 1) }, (_, lesson) => lesson + 1)
                    .filter((lesson) => !taken.has(lesson))
                    .map((lesson) => ({ value: String(lesson), label: text.lesson(format.number(lesson)) }))} />
              </Field>
              <Field id={`${idPrefix}-break-${index}-minutes`} label={text.duration(number)}>
                <Select id={`${idPrefix}-break-${index}-minutes`} value={String(slot.minutes)}
                  onChange={(event) => update(index, { ...slot, minutes: Number(event.target.value) })} options={minuteOptions(slot.minutes)} />
              </Field>
              <Button variant="ghost" size="sm" icon={<Trash2 aria-hidden="true" size={16} />} aria-label={text.remove(number)}
                onClick={() => onChange(sorted.filter((_, current) => current !== index), gapMinutes)}>{text.removeLabel}</Button>
            </li>
          );
        })}
      </ul>
      <div className="breaks-actions">
        <Field id={`${idPrefix}-gap`} label={text.gap}>
          <Select id={`${idPrefix}-gap`} value={String(gapMinutes)} onChange={(event) => onChange(sorted, Number(event.target.value))}
            options={gapMinuteChoices.map((minutes) => ({ value: String(minutes), label: minutes === 0 ? text.noGap : format.count(minutes, "minute") }))} />
        </Field>
        <Button variant="secondary" size="sm" icon={<Plus aria-hidden="true" size={16} />} disabled={sorted.length >= maxBreaks || position === null}
          onClick={() => position !== null && onChange([...sorted, { afterLesson: position, minutes: suggestedMinutes }], gapMinutes)}>{text.add}</Button>
        {sorted.length >= maxBreaks && <span className="card-note">{text.max}</span>}
      </div>
    </fieldset>
  );
}

/** Keeps only breaks that still fall between lessons (after the lesson count was lowered). */
export const validBreaks = (lessonCount: number, breaks: readonly BreakSlot[]) => breaks.filter((slot) => slot.afterLesson < lessonCount).slice(0, maxBreaks);
