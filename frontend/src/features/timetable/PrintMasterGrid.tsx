import { PrintDaysGrid } from "../../components/ui/print-days-grid";
import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import { weekdayLabel } from "../timetable-structure/weekdays";
import { clock, sessionOn, type GridLesson, type GridSection, type Term, type Timetable } from "./timetableApi";
import { cellFor, type Lookups } from "./TimetableGrids";

const text = messages.school.timetable;

/** Sections grouped by stage, in the order they appear (the timetable lists sections by stage). */
export function stageGroups(sections: GridSection[]): { stageName: string; sections: GridSection[] }[] {
  const groups: { stageName: string; sections: GridSection[] }[] = [];
  for (const section of sections) {
    const last = groups[groups.length - 1];
    if (last && last.stageName === section.stageName) last.sections.push(section);
    else groups.push({ stageName: section.stageName, sections: [section] });
  }
  return groups;
}

/**
 * The 12-hour time of lesson `number` on `day`: the day's session in the semester (R3), else the shift's own periods. Null when the sections
 * on the page are in different shifts (their clocks differ, so only the number is shown).
 */
export function lessonTime(timetable: Timetable, sections: GridSection[], term: Term, day: number, number: number, format: Formatter): string | null {
  const shiftIds = new Set(sections.map((section) => section.shiftId));
  if (shiftIds.size !== 1) return null;
  const [shiftId] = [...shiftIds];
  const sessions = timetable.sessions?.shiftId === shiftId ? timetable.sessions : null;
  const found = sessions
    ? sessions.timings.find((timing) => timing.session === sessionOn(sessions, term, day))?.lessons.find((lesson) => lesson.number === number)
    : timetable.shifts.find((shift) => shift.id === shiftId)?.lessons.find((lesson) => lesson.number === number);
  return found ? text.timeRange(format.time(clock(found.startMinute)), format.time(clock(found.endMinute))) : null;
}

type Props = {
  timetable: Timetable;
  /** The stages on this page (one, or all of them). */
  groups: { stageName: string; sections: GridSection[]; band: number }[];
  /** The days on this page (a chunk only when one stage alone is too tall for the page). */
  days: number[];
  lessonCount: number;
  look: Lookups;
  format: Formatter;
  term: Term;
  /** A stage header row over its sections (left out on a page of one stage: its title names the stage). */
  stageRows: boolean;
};

/** The printed whole school by days: rows are days × lessons, columns the sections of the stages on the page. */
export function PrintMasterGrid({ timetable, groups, days, lessonCount, look, format, term, stageRows }: Props) {
  const numbers = Array.from({ length: lessonCount }, (_, index) => index + 1);
  const at = new Map(timetable.lessons.map((lesson) => [`${lesson.sectionId}:${lesson.day}:${lesson.lesson}`, lesson]));
  const sections = groups.flatMap((group) => group.sections.map((section, index) => ({ section, stageStart: index === 0 && group !== groups[0] })));
  const sessions = timetable.sessions && sections.every(({ section }) => section.shiftId === timetable.sessions?.shiftId) ? timetable.sessions : null;
  const teacherOf = (lesson: GridLesson) => look.teacher(lesson.teacherId)?.shortName ?? "";
  return (
    <PrintDaysGrid
      caption={text.views.master}
      dayCorner={text.dayColumn}
      numberCorner={text.lessonCorner}
      stages={stageRows ? groups.map((group) => ({ key: group.stageName, title: group.stageName, span: group.sections.length, band: group.band })) : null}
      columns={sections.map(({ section, stageStart }) => ({ key: String(section.id), label: stageRows ? section.label : look.sectionName(section.id), stageStart }))}
      rows={days.flatMap((day) => numbers.map((number) => {
        const time = lessonTime(timetable, sections.map((item) => item.section), term, day, number, format);
        return {
          key: `${day}:${number}`,
          dayStart: number === 1,
          day: number === 1 ? { label: sessions ? text.dayWithSession(weekdayLabel(day), text.sessionShort[sessionOn(sessions, term, day)]) : weekdayLabel(day), span: numbers.length } : undefined,
          number: (
            <>
              <span>{format.number(number)}</span>
              {time && <span className="timetable-time">{time}</span>}
            </>
          ),
          cells: sections.map(({ section, stageStart }) => {
            const cell = cellFor(at.get(`${section.id}:${day}:${number}`), day, number, format, look, teacherOf, look.sectionName(section.id));
            return { key: String(section.id), subject: cell.subject, detail: cell.detail, color: cell.color, stageStart };
          }),
        };
      }))}
    />
  );
}
