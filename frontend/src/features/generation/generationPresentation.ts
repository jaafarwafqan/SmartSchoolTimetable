import { messages } from "../../i18n/messages";
import { isolate } from "../../i18n/isolate";
import type { Formatter } from "../../lib/format";
import type { GenerationRun, LiveProgress, RunStatus, SolverFinding } from "./generationApi";

const text = messages.school.generation;

export const phaseOrder: readonly LiveProgress["phase"][] = ["queued", "validating", "generating", "saving"];

/** The stepper position of a run: finished runs show every step done. */
export function phaseIndex(run: GenerationRun): number {
  if (run.live) return phaseOrder.indexOf(run.live.phase);
  if (run.status === "queued") return 0;
  if (run.status === "validating") return 1;
  if (run.status === "generating") return 2;
  return phaseOrder.length;
}

export type StatusTone = "neutral" | "primary" | "success" | "warning" | "danger";

export function statusTone(status: RunStatus): StatusTone {
  switch (status) {
    case "completed": return "success";
    case "cancelled":
    case "timedOut":
    case "interrupted": return "warning";
    case "failed":
    case "infeasible": return "danger";
    default: return "primary";
  }
}

export function statusNote(run: GenerationRun): string {
  switch (run.status) {
    case "completed": return run.optimal ? text.statusNotes.completedOptimal : text.statusNotes.completed;
    case "cancelled": return run.timetableVersionId ? text.statusNotes.cancelled : text.statusNotes.cancelledEmpty;
    case "failed": return text.statusNotes.failed;
    case "infeasible": return text.statusNotes.infeasible;
    case "timedOut": return text.statusNotes.timedOut;
    case "interrupted": return text.statusNotes.interrupted;
    default: return "";
  }
}

/** Seconds as a counted Arabic duration, rounded to a whole second (never an estimate). */
export function seconds(value: number, format: Formatter): string {
  return format.count(Math.max(0, Math.round(value)), "second");
}

/** Real elapsed time of an active run: now minus the server's start time (or the live solver clock when known). */
export function elapsedSeconds(run: GenerationRun, now: number): number | null {
  if (run.startedAt) return Math.max(0, (now - new Date(run.startedAt).getTime()) / 1000);
  return run.live?.elapsedSeconds ?? null;
}

export function diagnosticMessage(finding: SolverFinding, format: Formatter): string {
  const name = isolate(finding.entity.name);
  const required = finding.required ?? 0;
  const available = finding.available ?? 0;
  const lessons = (value: number) => format.count(value, "lesson");
  switch (finding.code) {
    case "CORE_TEACHER_AVAILABILITY": return text.diagnostics.CORE_TEACHER_AVAILABILITY(name, lessons(required), lessons(available));
    case "CORE_TEACHER_LIMITS": return text.diagnostics.CORE_TEACHER_LIMITS(name, lessons(required), lessons(available));
    case "CORE_SECTION_PACKING": return text.diagnostics.CORE_SECTION_PACKING(name);
    case "CORE_STAGE_DAYS": return text.diagnostics.CORE_STAGE_DAYS(name);
    case "CORE_SUBJECT_BLOCKED": return text.diagnostics.CORE_SUBJECT_BLOCKED(name);
    case "CORE_RESOURCE_CAPACITY": return text.diagnostics.CORE_RESOURCE_CAPACITY(name, lessons(required), format.count(available, "section"));
    case "CORE_SUBJECT_DAILY_CAP": return text.diagnostics.CORE_SUBJECT_DAILY_CAP(name);
    case "CORE_DOUBLE_PERIODS": return text.diagnostics.CORE_DOUBLE_PERIODS(name);
    case "CORE_LOCKED_LESSONS": return text.diagnostics.CORE_LOCKED_LESSONS(name);
    case "CORE_FUNDAMENTAL": return text.diagnostics.CORE_FUNDAMENTAL;
    case "TIMEOUT_NO_SOLUTION": return text.diagnostics.TIMEOUT_NO_SOLUTION(seconds(required, format));
    default: return text.unknownDiagnostic;
  }
}

/** Time-limit choices in seconds (10–600), labelled as durations. */
export const timeLimitChoices = [10, 20, 30, 45, 60, 90, 120, 180, 300, 600] as const;

const plain = text.plain;
type NoTimetableStatus = keyof typeof plain.noTimetable;

/** MF3: the result in plain Arabic — a headline and one friendly sentence; `ok` when a valid timetable was saved. */
export function resultHeadline(run: GenerationRun): { ok: boolean; title: string; sentence: string } {
  if (run.timetableVersionId !== null) {
    const sentence = run.status === "cancelled" ? plain.readyStopped : run.optimal ? plain.readyBest : plain.readyGood;
    return { ok: true, title: plain.ready, sentence };
  }
  const status: NoTimetableStatus = run.status in plain.noTimetable ? (run.status as NoTimetableStatus) : "failed";
  return { ok: false, title: plain.noTimetable[status], sentence: plain.noTimetableHint[status] };
}

/** MF3: one sentence per enabled soft rule that still has penalties («٤ فراغات في جداول المعلمين»), most weighted first. */
export function improvementNotes(run: GenerationRun, format: Formatter): { key: string; sentence: string }[] {
  const rules = [...(run.score?.rules ?? [])].filter((rule) => rule.enabled && rule.penalty > 0).sort((a, b) => b.weighted - a.weighted);
  return rules.flatMap((rule): { key: string; sentence: string }[] => {
    switch (rule.key) {
      case "avoidTeacherGaps": return [{ key: rule.key, sentence: plain.notes.avoidTeacherGaps(format.count(rule.penalty, "gap")) }];
      case "avoidSameSubjectRepeated": return [{ key: rule.key, sentence: plain.notes.avoidSameSubjectRepeated(format.count(rule.penalty, "time", "oblique")) }];
      case "spreadSubjectsAcrossDays": return [{ key: rule.key, sentence: plain.notes.spreadSubjectsAcrossDays(format.count(rule.penalty, "time", "oblique")) }];
      case "heavySubjectsEarly": return [{ key: rule.key, sentence: plain.notes.heavySubjectsEarly(format.count(rule.penalty, "lesson", "oblique")) }];
      case "keepDoubleLessonsTogether": return [{ key: rule.key, sentence: plain.notes.keepDoubleLessonsTogether(format.count(rule.penalty, "pair")) }];
      default: return [];
    }
  });
}
