import { CalendarClock, CalendarRange, Landmark, Megaphone } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "../../components/ui/badge";
import { Card } from "../../components/ui/card";
import { SectionTitle } from "../../components/ui/section-title";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useCalendarDays } from "../calendar/calendarApi";

const text = messages.school.dashboard.holiday;
const DAY_MS = 86_400_000;
const WINDOW_DAYS = 120;

const dayNumber = (date: string) => Math.floor(Date.parse(`${date}T00:00:00Z`) / DAY_MS);

/** MF8: the next official or school holiday (enabled entries only) within the coming months, with how far away it is. */
export function UpcomingHolidayCard() {
  const format = useFormatter();
  const today = format.today();
  const until = new Date((dayNumber(today) + WINDOW_DAYS) * DAY_MS).toISOString().slice(0, 10);
  const days = useCalendarDays({ from: today, to: until });
  const next = (days.data?.items ?? []).find((day) => day.isEnabled && (day.kind === "officialHoliday" || day.kind === "schoolHoliday"));
  const distance = next ? Math.max(0, dayNumber(next.startDate) - dayNumber(today)) : 0;
  const running = next !== undefined && next.startDate <= today;
  return (
    <Card className="dashboard-card" aria-labelledby="upcoming-holiday-title">
      <SectionTitle level={2} icon={Landmark} id="upcoming-holiday-title"><Link to="/school/calendar">{text.title}</Link></SectionTitle>
      {days.isSuccess && !next && <p className="card-note">{text.none}</p>}
      {next && (
        <div className="form-stack">
          <strong>{next.title}</strong>
          <span>{next.startDate === next.endDate ? format.date(next.startDate) : format.dateRange(next.startDate, next.endDate)}</span>
          <span className="row-actions">
            <Badge tone="primary" icon={<CalendarRange aria-hidden="true" size={16} />}>
              {running ? text.now : distance === 0 ? text.today : text.inDays(format.count(distance, "day"))}
            </Badge>
            {next.isApproximate && <Badge tone="warning" icon={<CalendarClock aria-hidden="true" size={16} />}>{messages.school.calendar.approximate}</Badge>}
            {next.byDecision && <Badge tone="warning" icon={<Megaphone aria-hidden="true" size={16} />}>{messages.school.calendar.byDecision}</Badge>}
          </span>
        </div>
      )}
    </Card>
  );
}
