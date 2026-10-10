import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import { weekdayLabel } from "../timetable-structure/weekdays";
import type { CurrentCheck, CurrentFinding, GridLesson, InputChange } from "./timetableApi";

const text = messages.school.currentCheck;

export type FindingItem = { key: string; sentence: string; sectionId: number | null; teacherId: number | null; day: number | null; lesson: number | null };
export type ChangeArea = keyof typeof text.areas;
export type ChangeItem = { key: string; area: ChangeArea; sentence: string };

type Names = CurrentCheck["names"];

const teacherName = (names: Names, id: number | null) => names.teachers.find((item) => item.id === id)?.name ?? text.unknown.teacher;
const subjectName = (names: Names, id: number | null) => names.subjects.find((item) => item.id === id)?.name ?? text.unknown.subject;
const stageName = (names: Names, id: number | null) => names.stages.find((item) => item.id === id)?.name ?? text.unknown.stage;
const resourceName = (names: Names, id: number | null) => names.resources.find((item) => item.id === id)?.name ?? text.unknown.resource;
const shiftName = (names: Names, id: number | null) => names.shifts.find((item) => item.id === id)?.name ?? text.unknown.shift;
function sectionName(names: Names, id: number | null): string {
  const section = names.sections.find((item) => item.id === id);
  return section ? messages.school.timetable.sectionName(section.stageName, section.label) : text.unknown.section;
}

/** The slots of a version's saved lessons that a finding points at: "section:day:lesson". */
export function slotKey(sectionId: number, day: number, lesson: number): string {
  return `${sectionId}:${day}:${lesson}`;
}

/**
 * The saved lessons marked on the grids: a finding with a slot marks that slot; one about a teacher on a day (a limit) marks the
 * teacher's lessons that day; one about a whole week marks all of the teacher's lessons. Colour is never the only mark (the cell shows an icon).
 */
export function flaggedSlots(findings: readonly CurrentFinding[], lessons: readonly GridLesson[]): Set<string> {
  const flagged = new Set<string>();
  for (const finding of findings) {
    if (finding.sectionId !== null && finding.day !== null && finding.lesson !== null) {
      flagged.add(slotKey(finding.sectionId, finding.day, finding.lesson));
    } else if (finding.sectionId !== null && finding.day !== null) {
      for (const lesson of lessons.filter((item) => item.sectionId === finding.sectionId && item.day === finding.day)) flagged.add(slotKey(lesson.sectionId, lesson.day, lesson.lesson));
    } else if (finding.teacherId !== null) {
      for (const lesson of lessons.filter((item) => item.teacherId === finding.teacherId && (finding.day === null || item.day === finding.day))) flagged.add(slotKey(lesson.sectionId, lesson.day, lesson.lesson));
    }
  }
  return flagged;
}

type Group = { key: string; findings: CurrentFinding[] };

function groupBy(findings: readonly CurrentFinding[], keyOf: (finding: CurrentFinding) => string): Group[] {
  const groups = new Map<string, CurrentFinding[]>();
  for (const finding of findings) groups.set(keyOf(finding), [...(groups.get(keyOf(finding)) ?? []), finding]);
  return [...groups.entries()].map(([key, items]) => ({ key, findings: items }));
}

/**
 * One Arabic sentence per problem (MF11). Several lessons of one cause become one sentence with a count: «محجوب يوم الأحد وفي الجدول ٤ حصص فيه»,
 * «مادة … في الشعبة … أصبحت للمعلم … والجدول ما زال باسم …».
 */
export function findingItems(check: CurrentCheck, format: Formatter): FindingItem[] {
  const { names } = check;
  const day = (value: number | null) => (value === null ? "" : weekdayLabel(value));
  const lessonNumber = (value: number | null) => format.number(value ?? 0);
  const items: FindingItem[] = [];
  const first = (group: Group) => group.findings[0];
  const item = (group: Group, sentence: string): FindingItem => ({
    key: group.key, sentence, sectionId: first(group).sectionId, teacherId: first(group).teacherId, day: first(group).day, lesson: first(group).lesson,
  });
  const sentences = text.findings;

  for (const group of groupBy(check.findings, (finding) => {
    switch (finding.code) {
      case "TEACHER_UNAVAILABLE": return `${finding.code}:${finding.teacherId}:${finding.day}`;
      case "TEACHER_REASSIGNED": return `${finding.code}:${finding.sectionId}:${finding.subjectId}:${finding.teacherId}:${finding.currentTeacherId}`;
      case "ASSIGNMENT_REMOVED": return `${finding.code}:${finding.sectionId}:${finding.subjectId}`;
      default: return `${finding.code}:${finding.sectionId}:${finding.teacherId}:${finding.subjectId}:${finding.resourceId}:${finding.day}:${finding.lesson}`;
    }
  })) {
    const finding = first(group);
    const count = format.count(group.findings.length, "lesson");
    switch (finding.code) {
      case "TEACHER_UNAVAILABLE":
        items.push(item(group, sentences.TEACHER_UNAVAILABLE(teacherName(names, finding.teacherId), day(finding.day), count)));
        break;
      case "TEACHER_CONFLICT":
        items.push(item(group, sentences.TEACHER_CONFLICT(teacherName(names, finding.teacherId), day(finding.day), lessonNumber(finding.lesson), sectionName(names, finding.sectionId))));
        break;
      case "TEACHER_DAY_LIMIT":
        items.push(item(group, sentences.TEACHER_DAY_LIMIT(teacherName(names, finding.teacherId), day(finding.day), format.count(finding.count ?? 0, "lesson"), format.number(finding.limit ?? 0))));
        break;
      case "TEACHER_WEEK_LIMIT":
        items.push(item(group, sentences.TEACHER_WEEK_LIMIT(teacherName(names, finding.teacherId), format.count(finding.count ?? 0, "lesson"), format.number(finding.limit ?? 0))));
        break;
      case "TEACHER_REASSIGNED":
        items.push(item(group, sentences.TEACHER_REASSIGNED(subjectName(names, finding.subjectId), sectionName(names, finding.sectionId),
          teacherName(names, finding.currentTeacherId), teacherName(names, finding.teacherId), count)));
        break;
      case "ASSIGNMENT_REMOVED":
        items.push(item(group, sentences.ASSIGNMENT_REMOVED(subjectName(names, finding.subjectId), sectionName(names, finding.sectionId), count)));
        break;
      case "WRONG_LESSON_COUNT":
        items.push(item(group, sentences.WRONG_LESSON_COUNT(subjectName(names, finding.subjectId), sectionName(names, finding.sectionId),
          format.count(finding.count ?? 0, "lesson"), format.count(finding.limit ?? 0, "lesson"))));
        break;
      case "OUTSIDE_SECTION_DAY":
        items.push(item(group, sentences.OUTSIDE_SECTION_DAY(subjectName(names, finding.subjectId), sectionName(names, finding.sectionId), day(finding.day), lessonNumber(finding.lesson), format.number(finding.limit ?? 0))));
        break;
      case "SECTION_CONFLICT":
        items.push(item(group, sentences.SECTION_CONFLICT(sectionName(names, finding.sectionId), day(finding.day), lessonNumber(finding.lesson))));
        break;
      case "SECTION_GAP":
        items.push(item(group, sentences.SECTION_GAP(sectionName(names, finding.sectionId), day(finding.day))));
        break;
      case "SUBJECT_BLOCKED":
        items.push(item(group, sentences.SUBJECT_BLOCKED(subjectName(names, finding.subjectId), sectionName(names, finding.sectionId), day(finding.day), lessonNumber(finding.lesson))));
        break;
      case "RESOURCE_CAPACITY":
        items.push(item(group, sentences.RESOURCE_CAPACITY(resourceName(names, finding.resourceId), day(finding.day), lessonNumber(finding.lesson), format.number(finding.count ?? 0), format.number(finding.limit ?? 0))));
        break;
      case "SUBJECT_DAILY_CAP":
        items.push(item(group, sentences.SUBJECT_DAILY_CAP(subjectName(names, finding.subjectId), sectionName(names, finding.sectionId), day(finding.day), format.count(finding.count ?? 0, "lesson"), format.number(finding.limit ?? 0))));
        break;
      case "DOUBLE_PERIOD_BROKEN":
        items.push(item(group, sentences.DOUBLE_PERIOD_BROKEN(subjectName(names, finding.subjectId), sectionName(names, finding.sectionId))));
        break;
      default:
        items.push(item(group, sentences.UNKNOWN));
    }
  }
  return items;
}

const areaOf: Record<string, ChangeArea> = {
  ASSIGNMENT_TEACHER_CHANGED: "assignments",
  ASSIGNMENT_ADDED: "assignments",
  ASSIGNMENT_REMOVED: "assignments",
  TEACHER_OFF_DAYS_CHANGED: "availability",
  TEACHER_BLOCKED_CHANGED: "availability",
  TEACHER_RELEASED_CHANGED: "availability",
  TEACHER_ARCHIVED_CHANGED: "availability",
  TEACHER_SPECIALIZATIONS_CHANGED: "availability",
  TEACHER_DAY_LIMIT_CHANGED: "loads",
  TEACHER_WEEK_LIMIT_CHANGED: "loads",
  LESSONS_PER_WEEK_CHANGED: "curriculum",
  LINE_ADDED: "curriculum",
  LINE_REMOVED: "curriculum",
  LINE_DOUBLE_CHANGED: "curriculum",
  WORKING_DAYS_CHANGED: "timing",
  SHIFT_TIMING_CHANGED: "timing",
  SECTION_ADDED: "timing",
  SECTION_REMOVED: "timing",
  SECTION_DAYS_CHANGED: "timing",
};

/** One Arabic sentence per change in the school data since the version was made, with the area it belongs to. */
export function changeItems(check: CurrentCheck, format: Formatter): ChangeItem[] {
  const { names } = check;
  const sentences = text.changes;
  const limit = (value: number | null) => (value === null ? text.noLimit : format.number(value));
  const days = (values: readonly number[] | null) => (values && values.length > 0 ? values.map(weekdayLabel).join("، ") : text.noDays);
  const sentenceOf = (change: InputChange): string => {
    switch (change.code) {
      case "ASSIGNMENT_TEACHER_CHANGED": return sentences.ASSIGNMENT_TEACHER_CHANGED(subjectName(names, change.subjectId), sectionName(names, change.sectionId), teacherName(names, change.teacherId), teacherName(names, change.toTeacherId));
      case "ASSIGNMENT_ADDED": return sentences.ASSIGNMENT_ADDED(subjectName(names, change.subjectId), sectionName(names, change.sectionId), teacherName(names, change.teacherId));
      case "ASSIGNMENT_REMOVED": return sentences.ASSIGNMENT_REMOVED(subjectName(names, change.subjectId), sectionName(names, change.sectionId), teacherName(names, change.teacherId));
      case "TEACHER_OFF_DAYS_CHANGED": return sentences.TEACHER_OFF_DAYS_CHANGED(teacherName(names, change.teacherId), days(change.days));
      case "TEACHER_BLOCKED_CHANGED": return sentences.TEACHER_BLOCKED_CHANGED(teacherName(names, change.teacherId), format.number(change.from ?? 0), format.number(change.to ?? 0));
      case "TEACHER_RELEASED_CHANGED": return sentences.TEACHER_RELEASED_CHANGED(teacherName(names, change.teacherId), change.to === 1);
      case "TEACHER_ARCHIVED_CHANGED": return sentences.TEACHER_ARCHIVED_CHANGED(teacherName(names, change.teacherId), change.to === 1);
      case "TEACHER_SPECIALIZATIONS_CHANGED": return sentences.TEACHER_SPECIALIZATIONS_CHANGED(teacherName(names, change.teacherId));
      case "TEACHER_DAY_LIMIT_CHANGED": return sentences.TEACHER_DAY_LIMIT_CHANGED(teacherName(names, change.teacherId), limit(change.from), limit(change.to));
      case "TEACHER_WEEK_LIMIT_CHANGED": return sentences.TEACHER_WEEK_LIMIT_CHANGED(teacherName(names, change.teacherId), limit(change.from), limit(change.to));
      case "LESSONS_PER_WEEK_CHANGED": return sentences.LESSONS_PER_WEEK_CHANGED(subjectName(names, change.subjectId), stageName(names, change.stageId), format.count(change.from ?? 0, "lesson"), format.count(change.to ?? 0, "lesson"));
      case "LINE_ADDED": return sentences.LINE_ADDED(subjectName(names, change.subjectId), stageName(names, change.stageId), format.count(change.to ?? 0, "lesson"));
      case "LINE_REMOVED": return sentences.LINE_REMOVED(subjectName(names, change.subjectId), stageName(names, change.stageId));
      case "LINE_DOUBLE_CHANGED": return sentences.LINE_DOUBLE_CHANGED(subjectName(names, change.subjectId), stageName(names, change.stageId), change.to === 1);
      case "WORKING_DAYS_CHANGED": return sentences.WORKING_DAYS_CHANGED(days(change.days));
      case "SHIFT_TIMING_CHANGED": return sentences.SHIFT_TIMING_CHANGED(shiftName(names, change.shiftId));
      case "SECTION_ADDED": return sentences.SECTION_ADDED(sectionName(names, change.sectionId));
      case "SECTION_REMOVED": return sentences.SECTION_REMOVED(sectionName(names, change.sectionId));
      case "SECTION_DAYS_CHANGED": return sentences.SECTION_DAYS_CHANGED(sectionName(names, change.sectionId));
      case "SUBJECT_RULES_CHANGED": return sentences.SUBJECT_RULES_CHANGED(subjectName(names, change.subjectId));
      case "RESOURCE_CHANGED": return sentences.RESOURCE_CHANGED(resourceName(names, change.resourceId), format.number(change.to ?? 0));
      case "PRIORITIES_CHANGED": return sentences.PRIORITIES_CHANGED;
      default: return sentences.OTHER_CHANGE;
    }
  };
  return check.changes.map((change, index) => ({ key: `${change.code}:${index}`, area: areaOf[change.code] ?? "other", sentence: sentenceOf(change) }));
}
