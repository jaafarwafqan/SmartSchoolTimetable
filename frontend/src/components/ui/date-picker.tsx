import { CalendarDays, ChevronLeft, ChevronRight, ChevronUp } from "lucide-react";
import { useState } from "react";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { monthGrid, monthStart } from "../../features/calendar/monthGrid";
import { weekdayLabel, weekdaysFrom } from "../../features/timetable-structure/weekdays";
import { Button } from "./button";

const text = messages.app.datePicker;

type DatePickerProps = {
  /** The chosen "yyyy-MM-dd" date, or "" when none. */
  value: string;
  weekStart: number;
  onPick: (date: string) => void;
  onClose: () => void;
};

/**
 * Month calendar for choosing a date instead of typing it (MF8): the month opens on the chosen date (or today), weeks start
 * on the school's week start day, and each day is a button named by its full Arabic date. It sits inline under the field,
 * so there is no floating layer to trap focus or hide behind a dialog.
 */
export function DatePicker({ value, weekStart, onPick, onClose }: DatePickerProps) {
  const format = useFormatter();
  const today = format.today();
  const [month, setMonth] = useState(() => monthStart(/^\d{4}-\d{2}-\d{2}$/.test(value) ? value : today));
  return (
    <div className="ui-date-picker" role="group" aria-label={text.label}>
      <div className="ui-date-picker-header">
        <Button variant="ghost" size="sm" icon={<ChevronRight aria-hidden="true" size={18} />} onClick={() => setMonth(monthStart(month, -1))}>{text.previousMonth}</Button>
        <strong>{format.month(month)}</strong>
        <Button variant="ghost" size="sm" icon={<ChevronLeft aria-hidden="true" size={18} />} onClick={() => setMonth(monthStart(month, 1))}>{text.nextMonth}</Button>
      </div>
      <div className="ui-date-picker-grid">
        {weekdaysFrom(weekStart).map((day) => <span key={`head-${day}`} className="ui-date-picker-head" aria-hidden="true">{weekdayLabel(day)}</span>)}
        {monthGrid(month, weekStart).flat().map((date, index) => date === null
          ? <span key={`blank-${index}`} aria-hidden="true" />
          : (
            <button key={date} type="button" className={`ui-date-picker-day${date === value ? " is-selected" : ""}${date === today ? " is-today" : ""}`}
              aria-pressed={date === value} aria-label={format.date(date)} onClick={() => onPick(date)}>
              {format.number(Number(date.slice(8)))}
            </button>
          ))}
      </div>
      <Button variant="secondary" size="sm" icon={<CalendarDays aria-hidden="true" size={18} />} onClick={() => onPick(today)}>{text.today}</Button>
      <Button variant="ghost" size="sm" icon={<ChevronUp aria-hidden="true" size={18} />} onClick={onClose}>{text.close}</Button>
    </div>
  );
}
