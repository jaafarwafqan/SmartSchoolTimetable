import { useLayoutEffect, type CSSProperties, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import { useSchoolContext } from "../../lib/schoolContext";
import { useSchoolProfile } from "../school-profile/profileApi";
import { blockSize, fitScale, pageCss, planPacking, planSchoolByDays, formatOf, formatSize, type PackPlan, type PrintOptions, type PageFormat } from "./printing";
import { PrintMasterGrid, stageGroups } from "./PrintMasterGrid";
import { weekdayLabel } from "../timetable-structure/weekdays";
import type { Term, Timetable } from "./timetableApi";
import { sessionViewOf, WeekGrid, type Lookups } from "./TimetableGrids";

const text = messages.school.printing;
const timetableText = messages.school.timetable;

/** One pageFormat of paper: its heading, the font scale, and either one grid or a packed set of blocks. */
type PageSpec = { key: string; heading: string; scale: number; grid?: ReactNode; blocks?: { key: string; title: string; grid: ReactNode }[]; columns?: number };

type Props = {
  timetable: Timetable;
  options: PrintOptions;
  /** The page shown on screen when the job is «العرض الحالي». */
  current: { view: "section" | "teacher" | "stage" | "master"; sectionId: number | undefined; teacherId: number | undefined; stageName: string | undefined };
  term: Term;
  format: Formatter;
  look: Lookups;
};

/**
 * The job's @page rule (paper, orientation, no margin) as a constructed stylesheet: the app's Content-Security-Policy blocks inline <style>
 * elements, and the CSSOM is allowed. A layout effect, so it is in place when printing starts.
 */
function usePageRule(pageFormat: PageFormat) {
  const css = pageCss(pageFormat);
  useLayoutEffect(() => {
    if (typeof CSSStyleSheet === "undefined" || !("adoptedStyleSheets" in document)) return undefined;
    const pageStyle = new CSSStyleSheet();
    pageStyle.replaceSync(css);
    document.adoptedStyleSheets = [...document.adoptedStyleSheets, pageStyle];
    return () => { document.adoptedStyleSheets = document.adoptedStyleSheets.filter((item) => item !== pageStyle); };
  }, [css]);
}

/** «الأيام: الأحد – الثلاثاء» for a page that carries some of the week's days. */
function daysHeading(days: number[]): string {
  const first = weekdayLabel(days[0] ?? 1);
  const last = weekdayLabel(days[days.length - 1] ?? 1);
  return days.length > 1 ? text.daysRange(first, last) : text.dayOnly(first);
}

function chunk<T>(items: T[], size: number): T[][] {
  const out: T[][] = [];
  for (let index = 0; index < items.length; index += Math.max(1, size)) out.push(items.slice(index, index + Math.max(1, size)));
  return out;
}

/**
 * MF5 and the owner's 1.0.0 testing: the official printed timetable, rendered beside the app root and visible only in print. Every pageFormat is
 * one `.print-page` exactly the size of the paper, padded by the document itself (the @page has no margin, so the browser has no room for its
 * own date, title and address), with the school logo and name, «جدول الدروس الأسبوعي», the year and semester, the entity, a signature
 * footer and «صفحة ١ من ٣». The whole school prints BY DAYS on one A3 page when it fits at a readable size; teachers and sections are packed
 * several to a page, never one split over two.
 */
export function PrintDocument({ timetable, options, current, term, format, look }: Props) {
  const school = useSchoolContext().data;
  const profile = useSchoolProfile().data;
  const shifts = new Map(timetable.shifts.map((shift) => [shift.id, shift]));
  const lessonCountOf = (shiftId: number) => Math.max(1, shifts.get(shiftId)?.lessons.length ?? 0,
    ...timetable.sections.filter((item) => item.shiftId === shiftId).flatMap((item) => item.allowedByDay.map((day) => day.lessons)));
  const headerRowsOf = (shiftId: number) => 1 + (sessionViewOf(timetable, shiftId, term)?.sessions.timings.length ?? 0);

  const sectionGrid = (sectionId: number) => {
    const section = timetable.sections.find((item) => item.id === sectionId);
    if (!section) return null;
    return (
      <WeekGrid caption={look.sectionName(section.id)} days={timetable.days} shift={shifts.get(section.shiftId)} lessonCount={lessonCountOf(section.shiftId)} format={format} look={look}
        lessons={timetable.lessons.filter((lesson) => lesson.sectionId === section.id)} sessions={sessionViewOf(timetable, section.shiftId, term)}
        secondLine={(lesson) => look.teacher(lesson.teacherId)?.shortName ?? ""} />
    );
  };
  const teacherShifts = (teacherId: number) => [...new Set(timetable.lessons.filter((lesson) => lesson.teacherId === teacherId).map((lesson) => look.section(lesson.sectionId)?.shiftId ?? 0))];
  const teacherGrid = (teacherId: number) => {
    const teacher = timetable.teachers.find((item) => item.id === teacherId);
    if (!teacher) return null;
    const lessons = timetable.lessons.filter((lesson) => lesson.teacherId === teacher.id);
    return teacherShifts(teacher.id).map((shiftId) => (
      <WeekGrid key={shiftId} caption={teacher.name} days={timetable.days} shift={shifts.get(shiftId)} lessonCount={lessonCountOf(shiftId)} format={format} look={look}
        lessons={lessons.filter((lesson) => look.section(lesson.sectionId)?.shiftId === shiftId)} sessions={sessionViewOf(timetable, shiftId, term)}
        secondLine={(lesson) => look.sectionName(lesson.sectionId)} />
    ));
  };
  const sectionTitle = (sectionId: number) => [text.section(look.sectionName(sectionId)),
    text.weekly(format.count(timetable.lessons.filter((lesson) => lesson.sectionId === sectionId).length, "lesson"))].join(" — ");
  const teacherTitle = (teacherId: number) => [text.teacher(timetable.teachers.find((item) => item.id === teacherId)?.name ?? ""),
    text.load(format.count(timetable.lessons.filter((lesson) => lesson.teacherId === teacherId).length, "lesson"))].join(" — ");

  // Packed batches: every block the same size (the largest grid of the batch), as many on a pageFormat as the plan allows.
  const packed = (kind: "sections" | "teachers"): { plan: PackPlan; pages: PageSpec[] } => {
    const ids = kind === "sections" ? timetable.sections.map((item) => item.id) : timetable.teachers.map((item) => item.id);
    const shiftsOf = (id: number) => (kind === "sections" ? [look.section(id)?.shiftId ?? 0] : teacherShifts(id));
    const columns = Math.max(1, ...ids.flatMap((id) => shiftsOf(id).map(lessonCountOf))) + 1;
    const rows = Math.max(1, ...ids.map((id) => shiftsOf(id).reduce((sum, shiftId) => sum + timetable.days.length + headerRowsOf(shiftId) - 1, 0)));
    const plan = planPacking(options, blockSize(columns, rows, 1), ids.length);
    const pages = chunk(ids, plan.perPage).map((group, index) => ({
      key: `${kind}-${index}`,
      heading: kind === "sections" ? timetableText.views.section : timetableText.views.teacher,
      scale: plan.scale,
      columns: plan.columns,
      blocks: group.map((id) => ({ key: String(id), title: kind === "sections" ? sectionTitle(id) : teacherTitle(id), grid: kind === "sections" ? sectionGrid(id) : teacherGrid(id) })),
    }));
    return { plan, pages };
  };

  // The whole school by days (or a page per stage).
  const lessonCount = Math.max(1, ...timetable.sections.flatMap((section) => section.allowedByDay.map((day) => day.lessons)));
  const schoolPages = (stageName?: string): { pageFormat: PageFormat; pages: PageSpec[] } => {
    const groups = stageGroups(timetable.sections).map((group, index) => ({ ...group, band: (index % 6) + 1 }))
      .filter((group) => stageName === undefined || group.stageName === stageName);
    const plan = planSchoolByDays(stageName === undefined ? options : { ...options, scope: "stages" }, groups.map((group) => group.sections.length), timetable.days, lessonCount);
    const splitDays = plan.pages.some((page) => page.days.length < timetable.days.length);
    return {
      pageFormat: plan.format,
      pages: plan.pages.map((page, index) => {
        const set = page.stages.map((stage) => groups[stage]).filter((group): group is (typeof groups)[number] => group !== undefined);
        return {
          key: `school-${index}`,
          heading: [text.school, plan.perStage ? text.stage(set[0]?.stageName ?? "") : null, splitDays ? daysHeading(page.days) : null].filter(Boolean).join(" — "),
          scale: plan.scale,
          grid: <PrintMasterGrid timetable={timetable} groups={set} days={page.days} lessonCount={lessonCount} look={look} format={format} term={term} stageRows={!plan.perStage} />,
        };
      }),
    };
  };

  const kind = options.scope === "current" ? current.view : options.scope;
  let pageFormat: PageFormat = formatOf(options);
  let pages: PageSpec[];
  if (kind === "sections" || kind === "teachers") {
    const result = packed(kind);
    pageFormat = result.plan.format;
    pages = result.pages;
  } else if (kind === "master" || kind === "school" || kind === "stages" || kind === "stage") {
    // «صفحة لكل مرحلة» plans every stage at once (options.scope is "stages"); the stage view prints its own stage.
    const result = schoolPages(kind === "stage" ? current.stageName ?? timetable.sections[0]?.stageName : undefined);
    pageFormat = result.pageFormat;
    pages = result.pages;
  } else {
    const isSection = kind === "section";
    const id = isSection ? current.sectionId ?? timetable.sections[0]?.id ?? 0 : current.teacherId ?? timetable.teachers[0]?.id ?? 0;
    const columns = isSection ? lessonCountOf(look.section(id)?.shiftId ?? 0) + 1 : Math.max(1, ...teacherShifts(id).map(lessonCountOf)) + 1;
    pages = [{ key: `${kind}-${id}`, heading: isSection ? sectionTitle(id) : teacherTitle(id), scale: fitScale(options, columns), grid: isSection ? sectionGrid(id) : teacherGrid(id) }];
  }
  usePageRule(pageFormat);
  const [width, height] = formatSize(pageFormat);

  const semester = timetable.sessions ? timetableText.terms[term] : school?.currentTerm?.name;
  const today = format.date(format.today());
  return createPortal(
    <div className={`print-document${options.fit ? " is-fit" : ""}${options.color ? "" : " is-bw"}`} data-pages={pages.length} data-paper={`${pageFormat.paper} ${pageFormat.orientation}`}
      style={{ "--paper-width": `${width}mm`, "--paper-height": `${height}mm` } as CSSProperties}>
      {pages.map((page, index) => (
        <section key={page.key} className="print-page" style={{ "--print-scale": page.scale } as CSSProperties}>
          <header className="print-page-header">
            {profile?.hasLogo && <img className="print-logo" src="/api/v1/school-profile/logo" alt={text.logoAlt} />}
            <div className="print-titles">
              <strong className="print-school">{school?.schoolName ?? ""}</strong>
              <span className="print-title">{text.documentTitle}</span>
              <span>{[text.year(school?.currentYear?.name ?? ""), semester ? text.semester(semester) : null, text.version(format.number(timetable.summary.number))].filter(Boolean).join(" — ")}</span>
              <span className="print-entity">{page.heading}</span>
            </div>
          </header>
          {page.grid && <div className="print-grid">{page.grid}</div>}
          {page.blocks && (
            <div className="print-grid print-blocks" style={{ "--print-columns": page.columns ?? 1 } as CSSProperties}>
              {page.blocks.map((block) => (
                <article key={block.key} className="print-block">
                  <p className="print-block-title"><strong>{block.title}</strong></p>
                  {block.grid}
                </article>
              ))}
            </div>
          )}
          <footer className="print-page-footer">
            <span>{profile?.principalName ? text.principal(profile.principalName) : text.principalBlank}</span>
            <span className="print-line">{text.signature}</span>
            <span className="print-line">{text.stamp}</span>
            <span>{text.printedOn(today)}</span>
            <span className="print-page-number">{text.pageNumber(format.number(index + 1), format.number(pages.length))}</span>
          </footer>
        </section>
      ))}
    </div>,
    document.body,
  );
}
