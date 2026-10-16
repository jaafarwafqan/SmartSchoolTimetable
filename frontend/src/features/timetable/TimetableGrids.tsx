import type { SubjectColorIndex } from "../../components/ui/timetable-cell";
import { TimetableGrid, type GridCell } from "../../components/ui/timetable-grid";
import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import { weekdayLabel } from "../timetable-structure/weekdays";
import { clock, sessionOn, type GridLesson, type GridLessonTime, type GridSection, type GridSessions, type GridShift, type Term, type Timetable } from "./timetableApi";

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

/** «عرض: الأيام عمودياً» (owner, 1.0.0 testing; the default of the school view) or the older layout with the days across. */
export type MasterLayout = "byDays" | "bySections";

type MasterGridProps = {
  timetable: Timetable;
  format: Formatter;
  look: Lookups;
  term?: Term;
  flagged?: ReadonlySet<string>;
  layout?: MasterLayout;
  /** The sections shown (a stage view shows its own); all of them by default. */
  sections?: GridSection[];
  /** Builds the cell (the editor adds selection and conflicts); defaults to the read-only cell. */
  cell?: (lesson: GridLesson | undefined, sectionId: number, day: number, number: number) => GridCell;
  caption?: string;
};

/**
 * The whole school (or one stage): by days, rows are days × lessons (the day label spans its lessons; each row shows the lesson number and
 * its 12-hour time) and columns are the sections grouped by stage, with a thick rule between stages and between days; by sections, the older
 * layout with the days across. It scrolls inside its own container only.
 */
export function MasterGrid({ timetable, format, look, term = 1, flagged, layout = "byDays", sections: shown, cell, caption }: MasterGridProps) {
  const sections = shown ?? timetable.sections;
  const lessonCount = Math.max(1, ...sections.flatMap((section) => section.allowedByDay.map((day) => day.lessons)));
  const numbers = Array.from({ length: lessonCount }, (_, index) => index + 1);
  const at = new Map(timetable.lessons.map((lesson) => [`${lesson.sectionId}:${lesson.day}:${lesson.lesson}`, lesson]));
  const teacherOf = (lesson: GridLesson) => look.teacher(lesson.teacherId)?.shortName ?? "";
  // R3: with daily sessions each day shows its session in the semester, and its lesson numbers that session's clock.
  const sessions = timetable.sessions && sections.every((section) => section.shiftId === timetable.sessions?.shiftId) ? timetable.sessions : null;
  const dayHeader = (day: number) => (sessions ? text.dayWithSession(weekdayLabel(day), text.sessionShort[sessionOn(sessions, term, day)]) : weekdayLabel(day));
  const shiftIds = new Set(sections.map((section) => section.shiftId));
  const timeOf = (day: number, number: number) => {
    if (shiftIds.size !== 1) return undefined;
    const [shiftId] = [...shiftIds];
    return sessions
      ? sessions.timings.find((timing) => timing.session === sessionOn(sessions, term, day))?.lessons.find((lesson) => lesson.number === number)
      : timetable.shifts.find((shift) => shift.id === shiftId)?.lessons.find((lesson) => lesson.number === number);
  };
  const numberHeader = (day: number, number: number) => {
    const time = timeOf(day, number);
    return (
      <>
        <span>{format.number(number)}</span>
        {time && <span className="timetable-time">{timeRange(time, format)}</span>}
      </>
    );
  };
  const build = (sectionId: number, day: number, number: number): GridCell => {
    const lesson = at.get(`${sectionId}:${day}:${number}`);
    if (cell) return cell(lesson, sectionId, day, number);
    const built = cellFor(lesson, day, number, format, look, teacherOf, look.sectionName(sectionId));
    // MF11: a slot that conflicts with today's school data shows an icon and a border as well as its text.
    return flagged?.has(`${sectionId}:${day}:${number}`) ? { ...built, state: "conflict" as const, description: messages.school.currentCheck.cellConflict(built.description) } : built;
  };
  if (layout === "byDays") {
    const groups: { stageName: string; sections: GridSection[] }[] = [];
    for (const section of sections) {
      const last = groups[groups.length - 1];
      if (last && last.stageName === section.stageName) last.sections.push(section);
      else groups.push({ stageName: section.stageName, sections: [section] });
    }
    const starts = new Set(groups.slice(1).map((group) => group.sections[0]?.id));
    return (
      <TimetableGrid
        caption={caption ?? text.views.master}
        groupCorner={text.dayColumn}
        corner={text.lessonCorner}
        className="timetable-master timetable-by-days"
        headerRows={[
          groups.map((group, index) => ({ key: `s${index}`, label: group.stageName, span: group.sections.length, boundary: index > 0 })),
          sections.map((section) => ({ key: String(section.id), label: section.label, boundary: starts.has(section.id) })),
        ]}
        rows={timetable.days.flatMap((day) => numbers.map((number) => ({
          key: `${day}:${number}`,
          group: number === 1 ? { label: dayHeader(day), span: numbers.length } : undefined,
          groupStart: number === 1,
          label: numberHeader(day, number),
          cells: sections.map((section) => ({ ...build(section.id, day, number), key: String(section.id), boundary: starts.has(section.id) })),
        })))}
      />
    );
  }
  return (
    <TimetableGrid
      caption={caption ?? text.views.master}
      corner={text.sectionColumn}
      className="timetable-master"
      headerRows={[
        timetable.days.map((day) => ({ key: `d${day}`, label: dayHeader(day), span: numbers.length })),
        timetable.days.flatMap((day) => numbers.map((number) => ({ key: `${day}:${number}`, label: sessions ? numberHeader(day, number) : format.number(number) }))),
      ]}
      rows={sections.map((section) => ({
        key: String(section.id),
        label: look.sectionName(section.id),
        cells: timetable.days.flatMap((day) => numbers.map((number) => build(section.id, day, number))),
      }))}
    />
  );
}
