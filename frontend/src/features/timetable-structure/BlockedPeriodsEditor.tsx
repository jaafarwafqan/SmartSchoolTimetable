import { CircleAlert } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { BlockedPeriodsGrid } from "../../components/ui/blocked-periods-grid";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useScheduleGrid, type BlockedSlot, type ScheduleGrid } from "./scheduleApi";
import { weekdayLabel } from "./weekdays";

const text = messages.school.blockedGrid;

/** Lessons that exist on a day (per-day counts, ADR 0020). */
export function lessonsOn(grid: ScheduleGrid | undefined, day: number): number {
  return grid?.lessonsByDay.find((entry) => entry.day === day)?.lessons ?? 0;
}

/** Keeps only slots inside the current grid (working days × that day's lessons); the server rejects the rest. */
export function slotsInGrid(slots: readonly BlockedSlot[], grid: ScheduleGrid | undefined): BlockedSlot[] {
  if (!grid) return [];
  return slots.filter((slot) => grid.days.includes(slot.day) && slot.lessonNumber >= 1 && slot.lessonNumber <= lessonsOn(grid, slot.day));
}

type BlockedPeriodsEditorProps = {
  id: string;
  value: readonly BlockedSlot[];
  onChange: (value: BlockedSlot[]) => void;
  error?: string;
};

/** Blocked-periods field for subjects and teachers: the keyboard grid plus its hint, empty and error states. */
export function BlockedPeriodsEditor({ id, value, onChange, error }: BlockedPeriodsEditorProps) {
  const format = useFormatter();
  const grid = useScheduleGrid();
  const inside = slotsInGrid(value, grid.data);
  const hasGrid = (grid.data?.days.length ?? 0) > 0 && (grid.data?.lessonsPerDay ?? 0) > 0;

  return (
    <section className="form-stack" aria-labelledby={`${id}-title`}>
      <h3 id={`${id}-title`}>{text.title}</h3>
      {grid.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {grid.isSuccess && !hasGrid && <Alert tone="info" message={text.noGrid} />}
      {hasGrid && inside.length < value.length && <Alert tone="warning" message={text.outsideGrid} />}
      {hasGrid && grid.data && (
        <>
          <p className="ui-field-hint" id={`${id}-hint`}>{text.hint}</p>
          <BlockedPeriodsGrid
            label={text.title}
            days={grid.data.days.map((day) => ({ day, name: weekdayLabel(day) }))}
            lessons={grid.data.lessonsPerDay}
            lessonLabel={(lesson) => text.lesson(format.number(lesson))}
            cellLabel={(day, lesson, blocked) => text.cell(day, format.number(lesson), blocked)}
            value={inside}
            onChange={onChange}
            isAvailable={(slot) => slot.lessonNumber <= lessonsOn(grid.data, slot.day)}
            unavailableLabel={(day, lesson) => text.unavailable(day, format.number(lesson))}
          />
        </>
      )}
      {error && (
        <p className="ui-field-error">
          <CircleAlert aria-hidden="true" size={16} />
          <span>{error}</span>
        </p>
      )}
    </section>
  );
}
