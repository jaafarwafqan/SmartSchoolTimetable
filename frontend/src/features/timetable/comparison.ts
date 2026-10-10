import { messages } from "../../i18n/messages";
import { isolate } from "../../i18n/isolate";
import type { Formatter } from "../../lib/format";
import { weekdayLabel } from "../timetable-structure/weekdays";
import type { Lookups } from "./TimetableGrids";
import type { LessonChange } from "./timetableApi";

const text = messages.school.lifecycle;
const slotText = messages.school.timetable;

/** What a grid cell shows about a comparison: where a lesson is now (moved, added, teacher changed) or where it used to be. */
export type SlotMark = { state: "moved" | "added" | "reassigned" | "was"; change: LessonChange };

const key = (day: number, lesson: number) => `${day}:${lesson}`;

/**
 * Marks the slots of one section's grid (the newer version). A moved, added or reassigned lesson marks its new slot; the
 * slot a moved or removed lesson left is marked «was» only when nothing else sits there now.
 * `occupied` holds the section's filled slots in the newer version as "day:lesson".
 */
export function marksForSection(changes: readonly LessonChange[], sectionId: number, occupied: ReadonlySet<string>): Map<string, SlotMark> {
  const marks = new Map<string, SlotMark>();
  const own = changes.filter((change) => change.sectionId === sectionId);
  for (const change of own) {
    if (change.kind !== "removed" && change.toDay !== null && change.toLesson !== null)
      marks.set(key(change.toDay, change.toLesson), { state: change.kind, change });
  }
  for (const change of own) {
    if ((change.kind === "moved" || change.kind === "removed") && change.fromDay !== null && change.fromLesson !== null) {
      const slot = key(change.fromDay, change.fromLesson);
      if (!occupied.has(slot) && !marks.has(slot)) marks.set(slot, { state: "was", change });
    }
  }
  return marks;
}

function slot(day: number | null, lesson: number | null, format: Formatter): string {
  return day === null || lesson === null ? "" : slotText.slotLabel(weekdayLabel(day), format.number(lesson));
}

/** One change as an Arabic sentence («الرياضيات: نُقلت من الأحد، الحصة ١ إلى ...»). */
export function describeChange(change: LessonChange, format: Formatter, look: Lookups): string {
  const subject = isolate(look.subject(change.subjectId)?.name ?? "");
  const teacherName = (id: number | null) => isolate(id === null ? "" : look.teacher(id)?.name ?? "");
  const from = slot(change.fromDay, change.fromLesson, format);
  const to = slot(change.toDay, change.toLesson, format);
  switch (change.kind) {
    case "added": return text.lineAdded(subject, to);
    case "removed": return text.lineRemoved(subject, from);
    case "reassigned": return text.lineReassigned(subject, to, teacherName(change.fromTeacherId), teacherName(change.toTeacherId));
    default: return change.fromTeacherId !== change.toTeacherId
      ? text.lineMovedTeacher(subject, from, to, teacherName(change.fromTeacherId), teacherName(change.toTeacherId))
      : text.lineMoved(subject, from, to);
  }
}
