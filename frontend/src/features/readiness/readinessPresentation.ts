import { messages } from "../../i18n/messages";
import { isolate } from "../../i18n/isolate";
import type { Formatter } from "../../lib/format";
import type { ReadinessFinding } from "./readinessApi";

const text = messages.school.readiness;

export type FindingGroup = {
  key: string;
  entity: ReadinessFinding["entity"];
  findings: ReadinessFinding[];
  shortage: number;
};

export function groupFindings(findings: ReadinessFinding[]): FindingGroup[] {
  const groups = new Map<string, FindingGroup>();
  for (const finding of findings) {
    const key = `${finding.entity.kind}:${finding.entity.id}`;
    const group = groups.get(key) ?? { key, entity: finding.entity, findings: [], shortage: 0 };
    group.findings.push(finding);
    groups.set(key, group);
  }
  return [...groups.values()].map((group) => {
    const primaryTeacherOverload = group.findings.find((finding) => finding.code === "TEACHER_OVERLOAD")?.shortage;
    const shortage = primaryTeacherOverload ?? group.findings.reduce((total, finding) => total + (finding.shortage ?? 0), 0);
    return { ...group, shortage };
  }).sort((first, second) => first.entity.kind.localeCompare(second.entity.kind) || first.entity.id - second.entity.id);
}

export function findingMessage(finding: ReadinessFinding, format: Formatter): string {
  const name = isolate(finding.entity.name);
  const required = finding.required ?? 0;
  const available = finding.available ?? 0;
  const shortage = finding.shortage ?? 0;
  switch (finding.code) {
    case "NOTHING_TO_SCHEDULE": return text.messages.NOTHING_TO_SCHEDULE;
    case "UNASSIGNED_LINES": return text.messages.UNASSIGNED_LINES(format.count(required, "line"), finding.details.map(isolate).join("، "));
    case "SECTION_OVER_CAPACITY": return text.messages.SECTION_OVER_CAPACITY(name, format.count(required, "lesson"), format.count(available, "lesson"), format.count(shortage, "lesson"));
    case "SECTION_UNDER_CAPACITY": return text.messages.SECTION_UNDER_CAPACITY(name, format.count(required, "lesson"), format.count(available, "lesson"));
    case "TEACHER_OVERLOAD": return text.messages.TEACHER_OVERLOAD(name, format.count(required, "lesson"), format.number(available), format.count(shortage, "lesson"));
    case "TEACHER_RELEASED_ASSIGNED": return text.messages.TEACHER_RELEASED_ASSIGNED(name, format.count(required, "lesson"));
    case "TEACHER_ARCHIVED_ASSIGNED": return text.messages.TEACHER_ARCHIVED_ASSIGNED(name, format.count(required, "lesson"));
    case "TEACHER_PARTIAL_RELEASE": return text.messages.TEACHER_PARTIAL_RELEASE(name);
    case "SUBJECT_SLOTS_SHORT": return text.messages.SUBJECT_SLOTS_SHORT(name, format.number(required), format.number(available), format.number(shortage));
    case "ASSIGNMENT_INFEASIBLE": return text.messages.ASSIGNMENT_INFEASIBLE(name, format.count(required, "lesson"), format.count(available, "lesson"), format.count(shortage, "lesson"));
    case "RESOURCE_OVER_CAPACITY": return text.messages.RESOURCE_OVER_CAPACITY(name, isolate(finding.details[0] ?? ""), format.count(required, "lesson"), format.count(available, "lesson"), format.count(shortage, "lesson"));
    case "RESOURCE_ARCHIVED": return text.messages.RESOURCE_ARCHIVED(name);
    case "DOUBLE_PERIOD_IMPOSSIBLE": {
      const message = text.messages.DOUBLE_PERIOD_IMPOSSIBLE(name, format.count(required, "pair"), format.count(available, "pair"));
      // A warning in the standard mode: say that it blocks generation only in the double-lesson mode.
      return finding.severity === "warning" ? `${message} ${text.doublePeriodWarningNote}` : message;
    }
    case "DOUBLE_PERIOD_TIGHT": return text.messages.DOUBLE_PERIOD_TIGHT(name, format.count(required, "pair"));
    case "ORPHAN_BLOCKED_PERIODS": return text.messages.ORPHAN_BLOCKED_PERIODS(name, format.count(required, "lesson"));
    case "SHIFT_WITHOUT_PERIODS": return text.messages.SHIFT_WITHOUT_PERIODS(name);
    case "DISTRIBUTION_DISABLED_IN_CURRICULUM": return text.messages.DISTRIBUTION_DISABLED_IN_CURRICULUM(name);
    case "STAGE_WITHOUT_CURRICULUM": return text.messages.STAGE_WITHOUT_CURRICULUM(name);
    case "TEACHER_SHIFT_OVERLAP": return text.messages.TEACHER_SHIFT_OVERLAP(name);
    default: return text.unknownFinding;
  }
}

export function entityHref(kind: string): string {
  switch (kind) {
    case "section": return "/teachers/workload";
    case "stage": return "/classes/curriculum";
    case "teacher": return "/teachers/list";
    case "subject": return "/classes/subjects";
    case "resource": return "/classes/resources";
    case "shift": return "/school/timing";
    default: return "/";
  }
}

export function fixHref(fix: string): string {
  if (["assignTeachers", "moveWorkload", "changeTeacher"].includes(fix)) return "/teachers/workload";
  if (["reduceCurriculum", "completeCurriculum", "fillCurriculum", "reduceLessons"].includes(fix)) return "/classes/curriculum";
  if (["increaseLessons", "addPeriods"].includes(fix)) return "/school/timing";
  if (["raiseTeacherLimit", "checkRelease", "removeTeacherBlocks"].includes(fix)) return "/teachers/list";
  if (["removeSubjectBlocks", "enableDistribution", "turnOffDoublePeriod"].includes(fix)) return "/classes/subjects";
  if (["raiseResourceCapacity", "restoreResource"].includes(fix)) return "/classes/resources";
  if (fix === "raiseTimeLimit") return "/timetable/generate";
  if (fix === "reviewWarnings") return "/readiness";
  return "/school/timing";
}

/** «لا أخطاء» / «خطأ واحد» / «٣ أخطاء»: zero is said in words, never «٠ خطأ». */
export function errorCount(count: number, format: Formatter): string {
  return count === 0 ? text.noErrors : format.count(count, "error");
}

export function warningCount(count: number, format: Formatter): string {
  return count === 0 ? text.noWarnings : format.count(count, "warning");
}
