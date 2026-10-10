import { useLayoutEffect, type CSSProperties, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import { useSchoolContext } from "../../lib/schoolContext";
import { useSchoolProfile } from "../school-profile/profileApi";
import { fitScale, pageCss, type PrintOptions } from "./printing";
import type { Term, Timetable } from "./timetableApi";
import { MasterGrid, sessionViewOf, WeekGrid, type Lookups } from "./TimetableGrids";

const text = messages.school.printing;
const timetableText = messages.school.timetable;

type PageSpec = { key: string; heading: string; columns: number; grid: ReactNode };

type Props = {
  timetable: Timetable;
  options: PrintOptions;
  /** The page shown on screen when the job is «العرض الحالي». */
  current: { view: "section" | "teacher" | "master"; sectionId: number | undefined; teacherId: number | undefined };
  term: Term;
  format: Formatter;
  look: Lookups;
};

/**
 * The job's @page rule (paper, orientation, page numbers) as a constructed stylesheet: the app's Content-Security-Policy
 * blocks inline <style> elements, and the CSSOM is allowed. A layout effect, so it is in place when printing starts.
 */
function usePageRule(options: PrintOptions, arabicIndic: boolean) {
  const css = pageCss(options, arabicIndic);
  useLayoutEffect(() => {
    if (typeof CSSStyleSheet === "undefined" || !("adoptedStyleSheets" in document)) return undefined;
    const pageStyle = new CSSStyleSheet();
    pageStyle.replaceSync(css);
    document.adoptedStyleSheets = [...document.adoptedStyleSheets, pageStyle];
    return () => { document.adoptedStyleSheets = document.adoptedStyleSheets.filter((item) => item !== pageStyle); };
  }, [css]);
}

/**
 * MF5: the official printed timetable. Rendered beside the app root (never cut by the page layout) and visible only in
 * print: every page carries the school logo and name, «جدول الدروس الأسبوعي», the year and semester, the section or
 * teacher, and a signature footer (principal, signature, stamp, date); the page number sits in the bottom margin.
 * Cells keep black borders and plain text, so the result is clear with «رسومات الخلفية» turned off.
 */
export function PrintDocument({ timetable, options, current, term, format, look }: Props) {
  usePageRule(options, format.number(1) !== "1");
  const school = useSchoolContext().data;
  const profile = useSchoolProfile().data;
  const shifts = new Map(timetable.shifts.map((shift) => [shift.id, shift]));
  const lessonCountOf = (shiftId: number) => Math.max(1, shifts.get(shiftId)?.lessons.length ?? 0,
    ...timetable.sections.filter((item) => item.shiftId === shiftId).flatMap((item) => item.allowedByDay.map((day) => day.lessons)));
  const masterColumns = 1 + timetable.days.length * Math.max(1, ...timetable.sections.flatMap((section) => section.allowedByDay.map((day) => day.lessons)));

  const sectionPage = (sectionId: number): PageSpec | null => {
    const section = timetable.sections.find((item) => item.id === sectionId);
    if (!section) return null;
    const count = lessonCountOf(section.shiftId);
    return {
      key: `section-${section.id}`, heading: text.section(look.sectionName(section.id)), columns: count + 1,
      grid: <WeekGrid caption={look.sectionName(section.id)} days={timetable.days} shift={shifts.get(section.shiftId)} lessonCount={count} format={format} look={look}
        lessons={timetable.lessons.filter((lesson) => lesson.sectionId === section.id)} sessions={sessionViewOf(timetable, section.shiftId, term)}
        secondLine={(lesson) => look.teacher(lesson.teacherId)?.shortName ?? ""} />,
    };
  };
  const teacherPage = (teacherId: number): PageSpec | null => {
    const teacher = timetable.teachers.find((item) => item.id === teacherId);
    if (!teacher) return null;
    const lessons = timetable.lessons.filter((lesson) => lesson.teacherId === teacher.id);
    const shiftIds = [...new Set(lessons.map((lesson) => look.section(lesson.sectionId)?.shiftId ?? 0))];
    return {
      key: `teacher-${teacher.id}`, heading: text.teacher(teacher.name), columns: Math.max(...shiftIds.map(lessonCountOf), 1) + 1,
      grid: shiftIds.map((shiftId) => (
        <WeekGrid key={shiftId} caption={teacher.name} days={timetable.days} shift={shifts.get(shiftId)} lessonCount={lessonCountOf(shiftId)} format={format} look={look}
          lessons={lessons.filter((lesson) => look.section(lesson.sectionId)?.shiftId === shiftId)} sessions={sessionViewOf(timetable, shiftId, term)}
          secondLine={(lesson) => look.sectionName(lesson.sectionId)} />
      )),
    };
  };
  const schoolPage = (): PageSpec => ({
    key: "school", heading: text.school, columns: masterColumns, grid: <MasterGrid timetable={timetable} format={format} look={look} term={term} />,
  });

  const kind = options.scope === "current" ? current.view : options.scope;
  const pages: PageSpec[] = (kind === "sections" ? timetable.sections.map((section) => sectionPage(section.id))
    : kind === "teachers" ? timetable.teachers.map((teacher) => teacherPage(teacher.id))
    : kind === "section" ? [sectionPage(current.sectionId ?? timetable.sections[0]?.id ?? 0)]
    : kind === "teacher" ? [teacherPage(current.teacherId ?? timetable.teachers[0]?.id ?? 0)]
    : [schoolPage()]).filter((page): page is PageSpec => page !== null);

  const semester = timetable.sessions ? timetableText.terms[term] : school?.currentTerm?.name;
  const today = format.date(format.today());
  return createPortal(
    <div className={`print-document${options.fit ? " is-fit" : ""}`} data-pages={pages.length}>
      {pages.map((page) => (
        <section key={page.key} className="print-page" style={{ "--print-scale": fitScale(options, page.columns) } as CSSProperties}>
          <header className="print-page-header">
            {profile?.hasLogo && <img className="print-logo" src="/api/v1/school-profile/logo" alt={text.logoAlt} />}
            <div className="print-titles">
              <strong className="print-school">{school?.schoolName ?? ""}</strong>
              <span className="print-title">{text.documentTitle}</span>
              <span>{[text.year(school?.currentYear?.name ?? ""), semester ? text.semester(semester) : null, text.version(format.number(timetable.summary.number))].filter(Boolean).join(" — ")}</span>
              <span className="print-entity">{page.heading}</span>
            </div>
          </header>
          <div className="print-grid">{page.grid}</div>
          <footer className="print-page-footer">
            <span>{profile?.principalName ? text.principal(profile.principalName) : text.principalBlank}</span>
            <span className="print-line">{text.signature}</span>
            <span className="print-line">{text.stamp}</span>
            <span>{text.printedOn(today)}</span>
          </footer>
        </section>
      ))}
    </div>,
    document.body,
  );
}
