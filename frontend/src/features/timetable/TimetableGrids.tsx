import type { SubjectColorIndex } from "../../components/ui/timetable-cell";
import { TimetableGrid, type GridCell } from "../../components/ui/timetable-grid";
import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import { weekdayLabel } from "../timetable-structure/weekdays";
import { clock, sessionOn, type GridLesson, type GridLessonTime, type GridSessions, type GridShift, type Term, type Timetable } from "./timetableApi";

const text = messages.school.timetable;

export function colorOf(index: number | undefined): SubjectColorIndex | undefined {
  return index && index >= 1 && index <= 10 ? (index as SubjectColorIndex) : undefined;
}

export type Lookups = {
  subject: (id: number) => { name: string; colorIndex: number } | undefined;
  teacher: (id: number) => { name: string; shortName: string } | undefined;
  section: (id: number) => { label: string; stageName: string; shiftId: number } | undefined;
  sectionName: (id: number) => string;
};

export function lookups(timetable: Timetable): Lookups {
  const subjects = new Map(timetable.subjects.map((item) => [item.id, item]));
  const teachers = new Map(timetable.teachers.map((item) => [item.id, item]));
  const sections = new Map(timetable.sections.map((item) => [item.id, item]));
  return {
    subject: (id) => subjects.get(id),
    teacher: (id) => teachers.get(id),
    section: (id) => sections.get(id),
    sectionName: (id) => {
      const section = sections.get(id);
      return section ? text.sectionName(section.stageName, section.label) : "";
    },
  };
}

function timeRange(time: GridLessonTime | undefined, format: Formatter) {
  return time ? text.timeRange(format.time(clock(time.startMinute)), format.time(clock(time.endMinute))) : "";
}

function lessonHeader(number: number, shift: GridShift | undefined, format: Formatter) {
  const time = shift?.lessons.find((lesson) => lesson.number === number);
  return (
    <>
      <span>{text.lessonColumn(format.number(number))}</span>
      {time && <span className="timetable-time">{timeRange(time, format)}</span>}
    </>
  );
}

/** R3: the daily sessions of a shift in one semester (the grid is the same; only the clock changes). */
export type SessionView = { sessions: GridSessions; term: Term };

/** The sessions of a grid's shift, when the timetable has them for that shift. */
export function sessionViewOf(timetable: Timetable, shiftId: number | undefined, term: Term): SessionView | undefined {
  return timetable.sessions && timetable.sessions.shiftId === shiftId ? { sessions: timetable.sessions, term } : undefined;
}

function dayLabel(day: number, view: SessionView | undefined) {
  if (!view) return weekdayLabel(day);
  return (
    <>
      <span>{weekdayLabel(day)}</span>
      <span className="timetable-session">{text.sessionShort[sessionOn(view.sessions, view.term, day)]}</span>
    </>
  );
}

/** One cell: the lesson's subject and second line, or a free period. */
export function cellFor(lesson: GridLesson | undefined, day: number, number: number, format: Formatter, look: Lookups,
  secondLine: (lesson: GridLesson) => string, prefix = ""): GridCell {
  const subject = lesson ? look.subject(lesson.subjectId) : undefined;
  const detail = lesson ? secondLine(lesson) : "";
  const description = lesson && subject
    ? text.cellLabel(weekdayLabel(day), format.number(number), subject.name, detail)
    : text.emptyCellLabel(weekdayLabel(day), format.number(number));
  return {
    key: `${day}:${number}`,
    subject: subject?.name,
    detail,
    color: colorOf(subject?.colorIndex),
    description: prefix ? text.withPrefix(prefix, description) : description,
  };
}

type WeekGridProps = {
  caption: string;
  days: number[];
  shift: GridShift | undefined;
  lessonCount: number;
  lessons: GridLesson[];
  format: Formatter;
  /** The second line of a cell: the teacher (section view) or the section (teacher view). */
  secondLine: (lesson: GridLesson) => string;
  look: Lookups;
  /** Builds the cell (the editor adds selection and conflicts); defaults to the read-only cell. */
  cell?: (lesson: GridLesson | undefined, day: number, number: number) => GridCell;
  /** R3 daily sessions: one clock row per session, and each day names its session in the chosen semester. */
  sessions?: SessionView;
};

/** Days × lessons for one section or one teacher in one shift. */
export function WeekGrid({ caption, days, shift, lessonCount, lessons, format, secondLine, look, cell, sessions }: WeekGridProps) {
  const at = new Map(lessons.map((lesson) => [`${lesson.day}:${lesson.lesson}`, lesson]));
  const numbers = Array.from({ length: lessonCount }, (_, index) => index + 1);
  const build = cell ?? ((lesson: GridLesson | undefined, day: number, number: number) => cellFor(lesson, day, number, format, look, secondLine));
  return (
    <TimetableGrid
      caption={caption}
      corner={text.dayColumn}
      headerRows={sessions
        ? [
            numbers.map((number) => ({ key: String(number), label: text.lessonColumn(format.number(number)) })),
            ...sessions.sessions.timings.map((timing) => numbers.map((number) => ({
              key: `${timing.session}:${number}`,
              label: <span className="timetable-time">{timeRange(timing.lessons.find((lesson) => lesson.number === number), format)}</span>,
            }))),
          ]
        : [numbers.map((number) => ({ key: String(number), label: lessonHeader(number, shift, format) }))]}
      headerRowLabels={sessions ? [null, ...sessions.sessions.timings.map((timing) => text.sessionNames[timing.session])] : undefined}
      rows={days.map((day) => ({
        key: String(day),
        label: dayLabel(day, sessions),
        cells: numbers.map((number) => build(at.get(`${day}:${number}`), day, number)),
      }))}
    />
  );
}

/** All sections × (days × lessons): the master timetable, scrolling inside its own container only. */
export function MasterGrid({ timetable, format, look, term = 1, flagged }: { timetable: Timetable; format: Formatter; look: Lookups; term?: Term; flagged?: ReadonlySet<string> }) {
  const lessonCount = Math.max(1, ...timetable.sections.flatMap((section) => section.allowedByDay.map((day) => day.lessons)));
  const numbers = Array.from({ length: lessonCount }, (_, index) => index + 1);
  const at = new Map(timetable.lessons.map((lesson) => [`${lesson.sectionId}:${lesson.day}:${lesson.lesson}`, lesson]));
  const teacherOf = (lesson: GridLesson) => look.teacher(lesson.teacherId)?.shortName ?? "";
  // R3: with daily sessions each day shows its session in the semester, and its lesson numbers that session's clock.
  const sessions = timetable.sessions && timetable.sections.every((section) => section.shiftId === timetable.sessions?.shiftId) ? timetable.sessions : null;
  const dayHeader = (day: number) => (sessions ? text.dayWithSession(weekdayLabel(day), text.sessionShort[sessionOn(sessions, term, day)]) : weekdayLabel(day));
  const numberHeader = (day: number, number: number) => {
    if (!sessions) return format.number(number);
    const time = sessions.timings.find((timing) => timing.session === sessionOn(sessions, term, day))?.lessons.find((lesson) => lesson.number === number);
    return (
      <>
        <span>{format.number(number)}</span>
        {time && <span className="timetable-time">{timeRange(time, format)}</span>}
      </>
    );
  };
  return (
    <TimetableGrid
      caption={text.views.master}
      corner={text.sectionColumn}
      className="timetable-master"
      headerRows={[
        timetable.days.map((day) => ({ key: `d${day}`, label: dayHeader(day), span: numbers.length })),
        timetable.days.flatMap((day) => numbers.map((number) => ({ key: `${day}:${number}`, label: numberHeader(day, number) }))),
      ]}
      rows={timetable.sections.map((section) => ({
        key: String(section.id),
        label: look.sectionName(section.id),
        cells: timetable.days.flatMap((day) => numbers.map((number) => {
          const cell = cellFor(at.get(`${section.id}:${day}:${number}`), day, number, format, look, teacherOf, look.sectionName(section.id));
          // MF11: a slot that conflicts with today's school data shows an icon and a border as well as its text.
          return flagged?.has(`${section.id}:${day}:${number}`) ? { ...cell, state: "conflict" as const, description: messages.school.currentCheck.cellConflict(cell.description) } : cell;
        })),
      }))}
    />
  );
}
