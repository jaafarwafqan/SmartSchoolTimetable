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
import { changedCount, moveOrSwap, push, redo, startHistory, undo, type History, type Slot } from "./editing";
import { checkTimetable, useSaveEdit, type GridLesson, type Timetable, type TimetableCheck, type Violation } from "./timetableApi";
import { cellFor, type Lookups, type SessionView, WeekGrid } from "./TimetableGrids";

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
    default: return violationText.unknown;
  }
}

/** Slots of this section (in its lessons) touched by a violation: marked as conflicts on the grid. */
function conflictSlots(violations: Violation[], lessons: GridLesson[], sectionId: number): Set<string> {
  const slots = new Set<string>();
  for (const violation of violations) {
    if (violation.day === null) continue;
    for (const lesson of lessons) {
      if (lesson.sectionId !== sectionId || lesson.day !== violation.day) continue;
      const sameSlot = violation.lesson === null || violation.lesson === lesson.lesson;
      const involves = violation.sectionId === sectionId || (violation.teacherId !== null && violation.teacherId === lesson.teacherId)
        || (violation.subjectId !== null && violation.subjectId === lesson.subjectId);
      if (sameSlot && involves) slots.add(`${lesson.day}:${lesson.lesson}`);
    }
  }
  return slots;
}

type EditorProps = {
  timetable: Timetable;
  sectionId: number;
  format: Formatter;
  look: Lookups;
  lessonCount: number;
  /** R3 daily sessions of the section's shift in the semester shown. */
  sessions?: SessionView;
  onClose: () => void;
  onSaved: (versionId: number) => void;
};

/**
 * «تعديل يدوي» (Phase 4 M4): move a lesson to an empty slot or swap two lessons of the section, by pointer, by
 * keyboard (Enter on a cell) or from the slot list. Every step is checked at once by the independent verifier;
 * saving is refused while any hard rule is broken, and a save creates a new version (the parent is kept).
 */
export function TimetableEditor({ timetable, sectionId, format, look, lessonCount, sessions, onClose, onSaved }: EditorProps) {
  const [history, setHistory] = useState<History>(() => startHistory(timetable.lessons));
  const [selected, setSelected] = useState<Slot | null>(null);
  const [target, setTarget] = useState("");
  const [note, setNote] = useState("");
  const [check, setCheck] = useState<TimetableCheck | null>(null);
  const [checking, setChecking] = useState(false);
  const save = useSaveEdit();
  const feedback = useFormFeedback();
  const lessons = history.present;
  const section = timetable.sections.find((item) => item.id === sectionId);
  const shift = timetable.shifts.find((item) => item.id === section?.shiftId);
  const days = timetable.days;

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

  const conflicts = useMemo(() => conflictSlots(check?.violations ?? [], lessons, sectionId), [check, lessons, sectionId]);
  const slotOptions = days.flatMap((day) => Array.from({ length: lessonCount }, (_, index) => index + 1)
    .map((lesson) => ({ value: `${day}:${lesson}`, label: text.slotLabel(weekdayLabel(day), format.number(lesson)) })));

  function apply(to: Slot) {
    if (!selected) return;
    setHistory((current) => push(current, moveOrSwap(current.present, selected, to)));
    setSelected(null);
    setTarget("");
  }

  function activate(day: number, lesson: number, filled: boolean) {
    const slot = { sectionId, day, lesson };
    if (!selected) {
      if (filled) setSelected(slot);
      return;
    }
    if (selected.day === day && selected.lesson === lesson) {
      setSelected(null);
      return;
    }
    apply(slot);
  }

  const teacherOf = (lesson: GridLesson) => look.teacher(lesson.teacherId)?.shortName ?? "";
  const violations = check?.violations ?? [];
  const selectedLesson = selected ? lessons.find((lesson) => lesson.sectionId === sectionId && lesson.day === selected.day && lesson.lesson === selected.lesson) : undefined;

  return (
    <div className="timetable-editor">
      <SectionTitle level={3} icon={Pencil}>{text.editTitle}</SectionTitle>
      <p className="card-note">{text.editHint}</p>
      <Alert tone="error" message={feedback.error} />
      <div className="timetable-editor-bar">
        <Button variant="secondary" aria-keyshortcuts="Control+Z" icon={<Undo2 aria-hidden="true" size={18} />} disabled={history.past.length === 0} onClick={() => { setSelected(null); setHistory(undo); }}>{text.undo}</Button>
        <Button variant="secondary" aria-keyshortcuts="Control+Y" icon={<Redo2 aria-hidden="true" size={18} />} disabled={history.future.length === 0} onClick={() => { setSelected(null); setHistory(redo); }}>{text.redo}</Button>
        <Badge>{text.changes(format.number(changedCount(timetable.lessons, lessons)))}</Badge>
      </div>
      <p className="card-note">{messages.school.lifecycle.undoRedoHint}</p>
      <p aria-live="polite">{selected && selectedLesson
        ? text.selected(`${look.subject(selectedLesson.subjectId)?.name ?? ""}، ${text.slotLabel(weekdayLabel(selected.day), format.number(selected.lesson))}`)
        : text.noSelection}</p>
      {selected && (
        <div className="timetable-editor-move">
          <Field id="timetable-move-target" label={text.moveTo}>
            <Select id="timetable-move-target" value={target} onChange={(event) => setTarget(event.target.value)}
              options={[{ value: "", label: text.chooseSlot }, ...slotOptions]} />
          </Field>
          <Button icon={<ArrowLeftRight aria-hidden="true" size={18} />} disabled={!target}
            onClick={() => { const [day, lesson] = target.split(":").map(Number); apply({ sectionId, day, lesson }); }}>{text.apply}</Button>
        </div>
      )}
      {section && (
        <WeekGrid caption={look.sectionName(section.id)} days={days} shift={shift} lessonCount={lessonCount} format={format} look={look} sessions={sessions}
          lessons={lessons.filter((lesson) => lesson.sectionId === section.id)} secondLine={teacherOf}
          cell={(lesson, day, number) => {
            const base = cellFor(lesson, day, number, format, look, teacherOf);
            const key = `${day}:${number}`;
            const isSelected = selected?.day === day && selected.lesson === number;
            return {
              ...base,
              state: isSelected ? "selected" : conflicts.has(key) ? "conflict" : "normal",
              onActivate: () => activate(day, number, lesson !== undefined),
            };
          }} />
      )}
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
