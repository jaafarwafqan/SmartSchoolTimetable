import { SectionTitle } from "../../components/ui/section-title";
import { SlidersHorizontal } from "lucide-react";
import { DateField } from "../../components/DateField";
import { TextField } from "../../components/TextField";
import { Checkbox } from "../../components/ui/checkbox";
import { messages } from "../../i18n/messages";
import type { FieldErrors } from "../../lib/useFormFeedback";
import { BlockedPeriodsEditor } from "../timetable-structure/BlockedPeriodsEditor";
import { useScheduleGrid, type BlockedSlot } from "../timetable-structure/scheduleApi";
import { weekdayLabel } from "../timetable-structure/weekdays";
import type { Teacher } from "./teachersApi";

const text = messages.school.teachers;

export type ConstraintState = { offDays: number[]; blocked: BlockedSlot[]; released: boolean };

type TeacherConstraintsProps = {
  /** Unique per open editor, so several rows can be expanded at once. */
  prefix: string;
  teacher: Teacher | null;
  state: ConstraintState;
  onChange: (state: ConstraintState) => void;
  errors: FieldErrors;
};

/** Constraints area: off-day toggles, blocked-periods grid, full release with dates, and lesson limits. */
export function TeacherConstraints({ prefix, teacher, state, onChange, errors }: TeacherConstraintsProps) {
  const grid = useScheduleGrid();
  const days = grid.data?.days ?? [];

  return (
    <section className="form-stack" aria-labelledby={`${prefix}-constraints-title`}>
      <SectionTitle level={3} icon={SlidersHorizontal} id={`${prefix}-constraints-title`}>{text.constraints}</SectionTitle>
      <fieldset className="weekday-grid" aria-describedby={errors.OffDays ? `${prefix}-off-days-error` : undefined}>
        <legend>{text.offDays}</legend>
        {days.map((day) => (
          <Checkbox key={day} checked={state.offDays.includes(day)} data-field="OffDays"
            onChange={(event) => onChange({ ...state, offDays: event.target.checked ? [...state.offDays, day] : state.offDays.filter((value) => value !== day) })}>
            {weekdayLabel(day)}
          </Checkbox>
        ))}
      </fieldset>
      {errors.OffDays && <p id={`${prefix}-off-days-error`} className="ui-field-error">{errors.OffDays}</p>}
      <BlockedPeriodsEditor id={`${prefix}-blocked`} value={state.blocked} onChange={(blocked) => onChange({ ...state, blocked })} error={errors.BlockedPeriods} />
      <Checkbox checked={state.released} onChange={(event) => onChange({ ...state, released: event.target.checked })}>{text.fullyReleased}</Checkbox>
      {state.released && (
        <div className="form-grid">
          <TextField id={`${prefix}-releaseReason`} label={text.releaseReason} defaultValue={teacher?.releaseReason ?? ""} maxLength={200} field="ReleaseReason" errors={errors} />
          <DateField id={`${prefix}-releaseFrom`} label={text.releaseFrom} defaultValue={teacher?.releaseFrom ?? ""} field="ReleaseFrom" errors={errors} />
          <DateField id={`${prefix}-releaseTo`} label={text.releaseTo} defaultValue={teacher?.releaseTo ?? ""} field="ReleaseTo" errors={errors} />
        </div>
      )}
      <p className="ui-field-hint">{text.limitsHint}</p>
      <div className="form-grid">
        <TextField id={`${prefix}-maxPerDay`} type="number" min={1} label={`${text.limits} ${text.maxPerDay}`} defaultValue={teacher?.maxLessonsPerDay?.toString() ?? ""} field="MaxLessonsPerDay" errors={errors} />
        <TextField id={`${prefix}-maxPerWeek`} type="number" min={1} label={`${text.limits} ${text.maxPerWeek}`} defaultValue={teacher?.maxLessonsPerWeek?.toString() ?? ""} field="MaxLessonsPerWeek" errors={errors} />
      </div>
    </section>
  );
}
