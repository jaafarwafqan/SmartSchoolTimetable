import { PrintMasterGrid as PrintMasterTable } from "../../components/ui/print-master-grid";
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

type Props = {
  timetable: Timetable;
  /** The stages on this page (one, or all of them). */
  groups: { stageName: string; sections: GridSection[] }[];
  /** The days on this page (a chunk when the whole week does not fit at a readable size). */
  days: number[];
  lessonCount: number;
  look: Lookups;
  format: Formatter;
  term: Term;
  /** A stage header row, a thick rule and a tinted section column per stage. */
  stageRows: boolean;
};

/** The printed whole-school grid for the given stages and days: builds the cells and headers, the table itself is the print primitive. */
export function PrintMasterGrid({ timetable, groups, days, lessonCount, look, format, term, stageRows }: Props) {
  const numbers = Array.from({ length: lessonCount }, (_, index) => index + 1);
  const at = new Map(timetable.lessons.map((lesson) => [`${lesson.sectionId}:${lesson.day}:${lesson.lesson}`, lesson]));
  const sessions = timetable.sessions && timetable.sections.every((section) => section.shiftId === timetable.sessions?.shiftId) ? timetable.sessions : null;
  const teacherOf = (lesson: GridLesson) => look.teacher(lesson.teacherId)?.shortName ?? "";
  const time = (day: number, number: number) => {
    const found = sessions?.timings.find((timing) => timing.session === sessionOn(sessions, term, day))?.lessons.find((lesson) => lesson.number === number);
    return found ? text.timeRange(format.time(clock(found.startMinute)), format.time(clock(found.endMinute))) : null;
  };
  return (
    <PrintMasterTable
      caption={text.views.master}
      corner={text.sectionColumn}
      dayHeaders={days.map((day) => ({
        key: String(day),
        span: lessonCount,
        label: sessions ? text.dayWithSession(weekdayLabel(day), text.sessionShort[sessionOn(sessions, term, day)]) : weekdayLabel(day),
      }))}
      numberHeaders={days.flatMap((day) => numbers.map((number) => ({
        key: `${day}:${number}`,
        label: (
          <>
            <span>{format.number(number)}</span>
            {time(day, number) && <span className="timetable-time">{time(day, number)}</span>}
          </>
        ),
      })))}
      groupRows={stageRows}
      groups={groups.map((group) => ({
        key: group.stageName,
        title: group.stageName,
        rows: group.sections.map((section) => ({
          key: String(section.id),
          label: look.sectionName(section.id),
          cells: days.flatMap((day) => numbers.map((number) => {
            const cell = cellFor(at.get(`${section.id}:${day}:${number}`), day, number, format, look, teacherOf, look.sectionName(section.id));
            return { key: `${day}:${number}`, subject: cell.subject, detail: cell.detail, color: cell.color };
          })),
        })),
      }))}
    />
  );
}
