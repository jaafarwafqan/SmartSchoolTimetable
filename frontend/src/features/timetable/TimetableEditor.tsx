import { SectionTitle } from "../../components/ui/section-title";
import { ArrowLeftRight, CircleAlert, CircleCheck, Redo2, Save, Undo2, X, Pencil } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Field } from "../../components/ui/field";
import { Input } from "../../components/ui/input";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { weekdayLabel } from "../timetable-structure/weekdays";
import { changedCount, conflictKeys, moveOrSwap, push, redo, startHistory, undo, type History, type Slot } from "./editing";
import { checkTimetable, useSaveEdit, type GridLesson, type Term, type Timetable, type TimetableCheck, type Violation } from "./timetableApi";
import { cellFor, MasterGrid, sessionViewOf, type Lookups, type MasterLayout, WeekGrid } from "./TimetableGrids";

const text = messages.school.timetable;
const violationText = messages.school.violations;

export function violationMessage(violation: Violation, format: Formatter, look: Lookups): string {
  const parts = [
    violation.sectionId ? look.sectionName(violation.sectionId) : violation.teacherId ? look.teacher(violation.teacherId)?.name ?? "" : "",
    violation.day ? weekdayLabel(violation.day) : "",
    violation.lesson ? text.lessonColumn(format.number(violation.lesson)) : "",
  ].filter(Boolean);
  const where = parts.join("، ");
  const count = format.number(violation.count ?? 0);
  const limit = format.number(violation.limit ?? 0);
  const messagesByCode = violationText.messages;
  switch (violation.code) {
    case "UNKNOWN_LESSON": return messagesByCode.UNKNOWN_LESSON(where);
    case "WRONG_LESSON_COUNT": return messagesByCode.WRONG_LESSON_COUNT(where, count, limit);
    case "SECTION_CONFLICT": return messagesByCode.SECTION_CONFLICT(where);
    case "OUTSIDE_SECTION_DAY": return messagesByCode.OUTSIDE_SECTION_DAY(where);
    case "SECTION_GAP": return messagesByCode.SECTION_GAP(where);
    case "TEACHER_CONFLICT": return messagesByCode.TEACHER_CONFLICT(where);
    case "TEACHER_UNAVAILABLE": return messagesByCode.TEACHER_UNAVAILABLE(where);
    case "SUBJECT_BLOCKED": return messagesByCode.SUBJECT_BLOCKED(where);
    case "TEACHER_DAY_LIMIT": return messagesByCode.TEACHER_DAY_LIMIT(where, count, limit);
    case "TEACHER_WEEK_LIMIT": return messagesByCode.TEACHER_WEEK_LIMIT(where, count, limit);
    case "RESOURCE_CAPACITY": return messagesByCode.RESOURCE_CAPACITY(where, count, limit);
    case "SUBJECT_DAILY_CAP": return messagesByCode.SUBJECT_DAILY_CAP(where, count, limit);
    case "DOUBLE_PERIOD_BROKEN": return messagesByCode.DOUBLE_PERIOD_BROKEN(where);
    case "TEACHER_CONSECUTIVE": return messagesByCode.TEACHER_CONSECUTIVE(where, format.count(violation.count ?? 0, "lesson"), format.count(violation.limit ?? 0, "lesson"));
    case "TEACHER_BALANCE": return messagesByCode.TEACHER_BALANCE(where, count, limit);
    default: return violationText.unknown;
  }
}

/** What the editor shows: one section, one teacher, one stage (all its sections) or the whole school. */
export type EditView = "section" | "teacher" | "stage" | "master";

type EditorProps = {
  timetable: Timetable;
  view: EditView;
  sectionId: number | undefined;
  teacherId: number | undefined;
  stageName: string | undefined;
  /** The school and stage views: by days (the default) or by sections. */
  layout: MasterLayout;
  term: Term;
  format: Formatter;
  look: Lookups;
  lessonCountOf: (shiftId: number) => number;
  onClose: () => void;
  onSaved: (versionId: number) => void;
};

/**
 * «تعديل يدوي» from ANY view (owner, 1.0.0 testing): one section, one teacher, a stage or the whole school. Choose a lesson, then an empty
 * slot of ITS section to move it there or a lesson of the same section to swap them: by pointer, by keyboard (Enter on a cell) or from the slot
 * list. In the teacher view a move happens in the lesson's section (the section's timetable changes with it). Every step is checked at once
 * by the independent verifier and its Arabic reason is listed (teacher busy, section busy, day off or blocked period, daily limit, lessons in
 * a row when that rule is hard…); saving is refused while any hard rule is broken, and a save creates a new version (the parent is kept).
 * The edit history (undo and redo) is one for all views: switching the view keeps it.
 */
export function TimetableEditor({ timetable, view, sectionId, teacherId, stageName, layout, term, format, look, lessonCountOf, onClose, onSaved }: EditorProps) {
  const [history, setHistory] = useState<History>(() => startHistory(timetable.lessons));
  const [selected, setSelected] = useState<Slot | null>(null);
  const [target, setTarget] = useState("");
  const [note, setNote] = useState("");
  const [check, setCheck] = useState<TimetableCheck | null>(null);
  const [checking, setChecking] = useState(false);
  const save = useSaveEdit();
  const feedback = useFormFeedback();
  const lessons = history.present;
  const days = timetable.days;
  const shifts = new Map(timetable.shifts.map((shift) => [shift.id, shift]));

  useEffect(() => {
    let current = true;
    setChecking(true);
    checkTimetable(timetable.summary.id, lessons)
      .then((result) => { if (current) setCheck(result); })
      .catch((error: unknown) => { if (current) feedback.showError(error); })
      .finally(() => { if (current) setChecking(false); });
    return () => { current = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lessons, timetable.summary.id]);

  // Undo and redo with the usual keys (Ctrl or Cmd + Z, Y, and Shift + Z). `code` keeps them working on an Arabic keyboard layout;
  // text fields keep their own undo.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (!(event.ctrlKey || event.metaKey) || event.altKey || event.defaultPrevented) return;
      if ((event.target as HTMLElement | null)?.closest("input, textarea, select, dialog")) return;
      const redoing = event.code === "KeyY" || (event.code === "KeyZ" && event.shiftKey);
      const undoing = event.code === "KeyZ" && !event.shiftKey;
      if (!redoing && !undoing) return;
      event.preventDefault();
      setSelected(null);
      setHistory(redoing ? redo : undo);
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  const conflicts = useMemo(() => conflictKeys(check?.violations ?? [], lessons), [check, lessons]);
  const lessonAt = (section: number, day: number, number: number) => lessons.find((lesson) => lesson.sectionId === section && lesson.day === day && lesson.lesson === number);
  const selectedLesson = selected ? lessonAt(selected.sectionId, selected.day, selected.lesson) : undefined;
  const selectedSection = selected ? look.section(selected.sectionId) : undefined;
  const slotOptions = selected
    ? days.flatMap((day) => Array.from({ length: lessonCountOf(selectedSection?.shiftId ?? 0) }, (_, index) => index + 1)
      .map((lesson) => ({ value: `${day}:${lesson}`, label: text.slotLabel(weekdayLabel(day), format.number(lesson)) })))
    : [];

  function apply(to: Slot) {
    if (!selected) return;
    setHistory((current) => push(current, moveOrSwap(current.present, selected, to)));
    setSelected(null);
    setTarget("");
  }

  /** A cell of a section (section, stage and school views). A lesson of another section than the chosen one becomes the new choice. */
  function activateSection(section: number, day: number, number: number) {
    const filled = lessonAt(section, day, number) !== undefined;
    if (!selected || selected.sectionId !== section) {
      setSelected(filled ? { sectionId: section, day, lesson: number } : null);
      return;
    }
    if (selected.day === day && selected.lesson === number) {
      setSelected(null);
      return;
    }
    apply({ sectionId: section, day, lesson: number });
  }

  /** A cell of the teacher view: the chosen lesson moves within its own section to that day and lesson. */
  function activateTeacher(lesson: GridLesson | undefined, day: number, number: number) {
    if (!selected) {
      if (lesson) setSelected({ sectionId: lesson.sectionId, day, lesson: number });
      return;
    }
    if (selected.day === day && selected.lesson === number) {
      setSelected(null);
      return;
    }
    apply({ sectionId: selected.sectionId, day, lesson: number });
  }

  const sectionCell = (lesson: GridLesson | undefined, section: number, day: number, number: number) => {
    const teacherOf = (item: GridLesson) => look.teacher(item.teacherId)?.shortName ?? "";
    const base = cellFor(lesson, day, number, format, look, teacherOf, view === "section" ? "" : look.sectionName(section));
    const key = `${section}:${day}:${number}`;
    const isSelected = selected?.sectionId === section && selected.day === day && selected.lesson === number;
    return { ...base, state: isSelected ? "selected" as const : conflicts.has(key) ? "conflict" as const : "normal" as const, onActivate: () => activateSection(section, day, number) };
  };

  const section = timetable.sections.find((item) => item.id === sectionId) ?? timetable.sections[0];
  const teacher = timetable.teachers.find((item) => item.id === teacherId) ?? timetable.teachers[0];
  const stageSections = timetable.sections.filter((item) => item.stageName === (stageName ?? timetable.sections[0]?.stageName));

  let grids;
  if (view === "teacher" && teacher) {
    const mine = lessons.filter((lesson) => lesson.teacherId === teacher.id);
    const shiftIds = [...new Set([...mine.map((lesson) => look.section(lesson.sectionId)?.shiftId ?? 0), ...(selectedSection ? [selectedSection.shiftId] : [])])];
    grids = shiftIds.map((shiftId) => (
      <WeekGrid key={shiftId} caption={teacher.name} days={days} shift={shifts.get(shiftId)} lessonCount={lessonCountOf(shiftId)} format={format} look={look}
        sessions={sessionViewOf(timetable, shiftId, term)} lessons={mine.filter((lesson) => look.section(lesson.sectionId)?.shiftId === shiftId)}
        secondLine={(lesson) => look.sectionName(lesson.sectionId)}
        cell={(lesson, day, number) => {
          const base = cellFor(lesson, day, number, format, look, (item) => look.sectionName(item.sectionId));
          const isSelected = selected?.day === day && selected.lesson === number && (selectedLesson?.teacherId === teacher.id) && look.section(selected.sectionId)?.shiftId === shiftId;
          const conflict = lesson !== undefined && conflicts.has(`${lesson.sectionId}:${day}:${number}`);
          return { ...base, state: isSelected ? "selected" as const : conflict ? "conflict" as const : "normal" as const, onActivate: () => activateTeacher(lesson, day, number) };
        }} />
    ));
  } else if (view === "stage" || view === "master") {
    grids = <MasterGrid timetable={{ ...timetable, lessons }} format={format} look={look} term={term} layout={layout}
      sections={view === "stage" ? stageSections : undefined} caption={view === "stage" ? text.stageCaption(stageSections[0]?.stageName ?? "") : undefined}
      cell={(lesson, id, day, number) => sectionCell(lesson, id, day, number)} />;
  } else if (section) {
    grids = (
      <WeekGrid caption={look.sectionName(section.id)} days={days} shift={shifts.get(section.shiftId)} lessonCount={lessonCountOf(section.shiftId)} format={format} look={look}
        sessions={sessionViewOf(timetable, section.shiftId, term)} lessons={lessons.filter((lesson) => lesson.sectionId === section.id)}
        secondLine={(lesson) => look.teacher(lesson.teacherId)?.shortName ?? ""}
        cell={(lesson, day, number) => sectionCell(lesson, section.id, day, number)} />
    );
  }

  const violations = check?.violations ?? [];
  return (
    <div className="timetable-editor">
      <SectionTitle level={3} icon={Pencil}>{text.editTitle}</SectionTitle>
      <p className="card-note">{view === "teacher" ? text.editHintTeacher : text.editHint}</p>
      <Alert tone="error" message={feedback.error} />
      <div className="timetable-editor-bar">
        <Button variant="secondary" aria-keyshortcuts="Control+Z" icon={<Undo2 aria-hidden="true" size={18} />} disabled={history.past.length === 0} onClick={() => { setSelected(null); setHistory(undo); }}>{text.undo}</Button>
        <Button variant="secondary" aria-keyshortcuts="Control+Y" icon={<Redo2 aria-hidden="true" size={18} />} disabled={history.future.length === 0} onClick={() => { setSelected(null); setHistory(redo); }}>{text.redo}</Button>
        <Badge>{text.changes(format.number(changedCount(timetable.lessons, lessons)))}</Badge>
      </div>
      <p className="card-note">{messages.school.lifecycle.undoRedoHint}</p>
      <p aria-live="polite">{selected && selectedLesson
        ? text.selected([look.subject(selectedLesson.subjectId)?.name ?? "", look.sectionName(selected.sectionId), text.slotLabel(weekdayLabel(selected.day), format.number(selected.lesson))].join("، "))
        : text.noSelection}</p>
      {selected && (
        <div className="timetable-editor-move">
          <Field id="timetable-move-target" label={text.moveTo}>
            <Select id="timetable-move-target" value={target} onChange={(event) => setTarget(event.target.value)}
              options={[{ value: "", label: text.chooseSlot }, ...slotOptions]} />
          </Field>
          <Button icon={<ArrowLeftRight aria-hidden="true" size={18} />} disabled={!target}
            onClick={() => { const [day, lesson] = target.split(":").map(Number); apply({ sectionId: selected.sectionId, day, lesson }); }}>{text.apply}</Button>
        </div>
      )}
      {grids}
      {checking && <p role="status">{text.checking}</p>}
      {check && violations.length === 0 && !checking && (
        <p className="generation-verified"><CircleCheck aria-hidden="true" size={18} /><span>{text.noConflicts}</span></p>
      )}
      {violations.length > 0 && (
        <section className="timetable-conflicts" aria-labelledby="timetable-conflicts-title">
          <h4 id="timetable-conflicts-title"><CircleAlert aria-hidden="true" size={18} /> {text.conflictsTitle(format.number(violations.length))}</h4>
          <ul>
            {violations.map((violation, index) => <li key={`${violation.code}-${index}`}>{violationMessage(violation, format, look)}</li>)}
          </ul>
        </section>
      )}
      <Field id="timetable-edit-note" label={text.note} hint={text.noteHint}>
        <Input id="timetable-edit-note" value={note} maxLength={200} aria-describedby="timetable-edit-note-hint" onChange={(event) => setNote(event.target.value)} />
      </Field>
      <div className="timetable-editor-bar">
        <Button icon={<Save aria-hidden="true" size={18} />} loading={save.isPending}
          disabled={checking || violations.length > 0 || changedCount(timetable.lessons, lessons) === 0}
          onClick={() => save.mutate({ id: timetable.summary.id, lessons, note: note.trim() || null }, {
            onSuccess: (version) => onSaved(version.id),
            onError: feedback.showError,
          })}>{text.saveEdit}</Button>
        <Button variant="secondary" icon={<X aria-hidden="true" size={18} />} onClick={onClose}>{text.cancelEdit}</Button>
      </div>
    </div>
  );
}
