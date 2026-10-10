import { SectionTitle } from "../../components/ui/section-title";
import { CalendarClock, CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useWorkingWeek } from "../timetable-structure/scheduleApi";
import { weekdayLabel, weekdaysFrom } from "../timetable-structure/weekdays";
import { useCalendarDays, type CalendarDay } from "./calendarApi";
import { kindIcons } from "./calendarKinds";
import { monthEnd, monthGrid, monthStart } from "./monthGrid";

const text = messages.school.calendar;

type MonthViewProps = { month: string; onMonth: (month: string) => void; onOpen: (day: CalendarDay) => void };

/** Month view: weeks start on the school's week start day; each entry opens its edit dialog. */
export function MonthView({ month, onMonth, onOpen }: MonthViewProps) {
  const format = useFormatter();
  const week = useWorkingWeek();
  const weekStart = week.data?.weekStartDay ?? 7;
  const days = useCalendarDays({ from: month, to: monthEnd(month) });
  const entries = days.data?.items ?? [];
  const entriesOn = (date: string) => entries.filter((entry) => entry.startDate <= date && entry.endDate >= date);

  return (
    <section className="month-view" aria-labelledby="month-title">
      <div className="card-header-row">
        <Button variant="ghost" icon={<ChevronRight aria-hidden="true" size={20} />} onClick={() => onMonth(monthStart(month, -1))}>{text.previousMonth}</Button>
        <SectionTitle level={2} icon={CalendarDays} id="month-title">{format.month(month)}</SectionTitle>
        <Button variant="ghost" icon={<ChevronLeft aria-hidden="true" size={20} />} onClick={() => onMonth(monthStart(month, 1))}>{text.nextMonth}</Button>
      </div>
      {days.isSuccess && entries.length === 0 && <Alert tone="info" message={text.emptyMonth} />}
      <div className="month-grid" role="list" aria-label={format.month(month)}>
        {weekdaysFrom(weekStart).map((day) => <div key={`head-${day}`} className="month-head" aria-hidden="true">{weekdayLabel(day)}</div>)}
        {monthGrid(month, weekStart).flat().map((date, index) => date === null
          ? <div key={`blank-${index}`} className="month-cell is-blank" aria-hidden="true" />
          : (
            <div key={date} className="month-cell" role="listitem" aria-label={format.date(date)}>
              <span className="month-day-number">{format.number(Number(date.slice(8)))}</span>
              {entriesOn(date).map((entry) => {
                const Icon = entry.isApproximate ? CalendarClock : kindIcons[entry.kind];
                return (
                  <Button key={entry.id} size="sm" variant="ghost" className={`month-entry kind-${entry.kind}${entry.isEnabled ? "" : " is-disabled"}`}
                    title={entry.isApproximate ? text.approximate : undefined} icon={<Icon aria-hidden="true" size={16} />} onClick={() => onOpen(entry)}>
                    {entry.title}
                  </Button>
                );
              })}
            </div>
          ))}
      </div>
    </section>
  );
}
