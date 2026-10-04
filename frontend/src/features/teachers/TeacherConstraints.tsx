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
  teacher: Teacher | null;
  state: ConstraintState;
  onChange: (state: ConstraintState) => void;
  errors: FieldErrors;
};

/** Constraints area: off-day toggles, blocked-periods grid, full release with dates, and lesson limits. */
export function TeacherConstraints({ teacher, state, onChange, errors }: TeacherConstraintsProps) {
  const grid = useScheduleGrid();
  const days = grid.data?.days ?? [];

  return (
    <section className="form-stack" aria-labelledby="teacher-constraints-title">
      <h3 id="teacher-constraints-title">{text.constraints}</h3>
      <fieldset className="weekday-grid" aria-describedby={errors.OffDays ? "teacher-off-days-error" : undefined}>
        <legend>{text.offDays}</legend>
        {days.map((day) => (
          <Checkbox key={day} checked={state.offDays.includes(day)} data-field="OffDays"
            onChange={(event) => onChange({ ...state, offDays: event.target.checked ? [...state.offDays, day] : state.offDays.filter((value) => value !== day) })}>
            {weekdayLabel(day)}
          </Checkbox>
        ))}
      </fieldset>
      {errors.OffDays && <p id="teacher-off-days-error" className="ui-field-error">{errors.OffDays}</p>}
      <BlockedPeriodsEditor id="teacher-blocked" value={state.blocked} onChange={(blocked) => onChange({ ...state, blocked })} error={errors.BlockedPeriods} />
      <Checkbox checked={state.released} onChange={(event) => onChange({ ...state, released: event.target.checked })}>{text.fullyReleased}</Checkbox>
      {state.released && (
        <div className="form-grid">
          <TextField id="releaseReason" label={text.releaseReason} defaultValue={teacher?.releaseReason ?? ""} maxLength={200} field="ReleaseReason" errors={errors} />
          <TextField id="releaseFrom" type="date" label={text.releaseFrom} defaultValue={teacher?.releaseFrom ?? ""} field="ReleaseFrom" errors={errors} />
          <TextField id="releaseTo" type="date" label={text.releaseTo} defaultValue={teacher?.releaseTo ?? ""} field="ReleaseTo" errors={errors} />
        </div>
      )}
      <p className="ui-field-hint">{text.limitsHint}</p>
      <div className="form-grid">
        <TextField id="maxPerDay" type="number" min={1} label={`${text.limits} ${text.maxPerDay}`} defaultValue={teacher?.maxLessonsPerDay?.toString() ?? ""} field="MaxLessonsPerDay" errors={errors} />
        <TextField id="maxPerWeek" type="number" min={1} label={`${text.limits} ${text.maxPerWeek}`} defaultValue={teacher?.maxLessonsPerWeek?.toString() ?? ""} field="MaxLessonsPerWeek" errors={errors} />
      </div>
    </section>
  );
}
