import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";

export type TimetableVersionSummary = {
  id: number;
  version: number;
  number: number;
  source: "generated" | "edited" | "rolledBack" | "repaired" | "teacherReplaced";
  generationRunId: number | null;
  parentVersionId: number | null;
  mode: "standard" | "doublePeriods";
  score: number | null;
  lessons: number;
  createdAt: string;
  isApproved: boolean;
  approvedAt: string | null;
  note: string | null;
  stale: boolean;
  /** M1 lifecycle: only a draft can still be approved; approved and archived versions never change. */
  status: TimetableStatus;
  archivedAt: string | null;
};

export type TimetableStatus = "draft" | "approved" | "archived";

export type GridLessonTime = { number: number; startMinute: number; endMinute: number };
export type GridShift = { id: number; name: string; lessons: GridLessonTime[] };
export type GridSection = { id: number; stageName: string; label: string; shiftId: number; allowedByDay: { day: number; lessons: number }[] };
export type GridSubject = { id: number; name: string; colorIndex: number };
export type GridTeacher = { id: number; name: string; shortName: string };
export type GridLesson = { sectionId: number; lineId: number; subjectId: number; teacherId: number; day: number; lesson: number };
export type SessionName = "morning" | "noon" | "evening";
/** R3 daily sessions: the clock of each session and the session of each working day per semester (null for one session). */
export type GridSessions = {
  system: "twoSessions" | "threeSessions";
  shiftId: number;
  timings: { session: SessionName; lessons: GridLessonTime[] }[];
  days: { term: 1 | 2; day: number; session: SessionName }[];
  /** The semester whose dates contain today; null when the year's dates do not tell (then semester 1). */
  currentTerm: 1 | 2 | null;
};
export type Term = 1 | 2;

export type Timetable = {
  summary: TimetableVersionSummary;
  days: number[];
  shifts: GridShift[];
  sections: GridSection[];
  subjects: GridSubject[];
  teachers: GridTeacher[];
  lessons: GridLesson[];
  score: { total: number; rules: { key: string; enabled: boolean; weight: number; penalty: number; weighted: number }[] } | null;
  violations: number;
  sessions: GridSessions | null;
};

export const timetableKeys = {
  versions: (yearId: number) => ["timetable", "versions", yearId] as const,
  version: (id: number) => ["timetable", "version", id] as const,
};

export function useTimetableVersions(yearId: number | undefined) {
  return useQuery({
    queryKey: timetableKeys.versions(yearId ?? 0),
    queryFn: () => apiRequest<TimetableVersionSummary[]>(`/api/v1/academic-years/${yearId}/timetables`),
    enabled: yearId !== undefined,
  });
}

export function useTimetable(id: number | undefined) {
  return useQuery({
    queryKey: timetableKeys.version(id ?? 0),
    queryFn: () => apiRequest<Timetable>(`/api/v1/timetables/${id}`),
    enabled: id !== undefined,
  });
}

/** A lifecycle change also changes what the generation screen offers to keep and what the history lists. */
function refreshLifecycle(client: QueryClient) {
  void client.invalidateQueries({ queryKey: ["timetable"] });
  void client.invalidateQueries({ queryKey: ["generation"] });
  void client.invalidateQueries({ queryKey: ["audit"] });
}

export function useApproveTimetable() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (version: TimetableVersionSummary) => apiRequest<TimetableVersionSummary>(`/api/v1/timetables/${version.id}/approve`, "POST", { version: version.version }),
    onSuccess: () => refreshLifecycle(client),
  });
}

export function useArchiveTimetable() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (version: TimetableVersionSummary) => apiRequest<TimetableVersionSummary>(`/api/v1/timetables/${version.id}/archive`, "POST", { version: version.version }),
    onSuccess: () => refreshLifecycle(client),
  });
}

/** Restores an older version as a NEW draft version (the older one never changes). */
export function useRollbackTimetable() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (version: TimetableVersionSummary) => apiRequest<TimetableVersionSummary>(`/api/v1/timetables/${version.id}/rollback`, "POST", { version: version.version, note: null }),
    onSuccess: () => refreshLifecycle(client),
  });
}

/** MF11: one problem of a saved version against today's data (names come from `CurrentCheck.names`). */
export type CurrentFinding = {
  code: string;
  sectionId: number | null;
  teacherId: number | null;
  currentTeacherId: number | null;
  subjectId: number | null;
  resourceId: number | null;
  day: number | null;
  lesson: number | null;
  count: number | null;
  limit: number | null;
};

/** MF11: one change in the scheduling input since the version was made. */
export type InputChange = {
  code: string;
  teacherId: number | null;
  toTeacherId: number | null;
  sectionId: number | null;
  subjectId: number | null;
  stageId: number | null;
  shiftId: number | null;
  resourceId: number | null;
  from: number | null;
  to: number | null;
  days: number[] | null;
};

type Named = { id: number; name: string };
export type CurrentCheck = {
  versionId: number;
  stale: boolean;
  findings: CurrentFinding[];
  changes: InputChange[];
  canReplaceTeachers: boolean;
  canRepair: boolean;
  names: {
    teachers: Named[];
    sections: { id: number; stageName: string; label: string }[];
    subjects: Named[];
    stages: Named[];
    resources: Named[];
    shifts: Named[];
  };
};

/** «الفحص على البيانات الحالية» (MF11): read fresh on every visit, because the school data may have changed meanwhile. */
export function useCurrentCheck(id: number | undefined) {
  return useQuery({
    queryKey: ["timetable", "current-check", id ?? 0],
    queryFn: () => apiRequest<CurrentCheck>(`/api/v1/timetables/${id}/current-check`),
    enabled: id !== undefined,
    refetchOnMount: "always",
  });
}

/** «استبدال المعلم في الجدول»: a NEW draft with today's teachers in the same slots. */
export function useReplaceTeachers() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (version: TimetableVersionSummary) => apiRequest<TimetableVersionSummary>(`/api/v1/timetables/${version.id}/replace-teachers`, "POST", { version: version.version }),
    onSuccess: () => refreshLifecycle(client),
  });
}

export type ChangeKind = "added" | "removed" | "moved" | "reassigned";

export type LessonChange = {
  kind: ChangeKind;
  sectionId: number;
  lineId: number;
  subjectId: number;
  fromTeacherId: number | null;
  toTeacherId: number | null;
  fromDay: number | null;
  fromLesson: number | null;
  toDay: number | null;
  toLesson: number | null;
};

export type Comparison = {
  fromVersionId: number;
  toVersionId: number;
  fromNumber: number;
  toNumber: number;
  totals: { added: number; removed: number; moved: number; reassigned: number; unchanged: number };
  sections: { sectionId: number; added: number; removed: number; moved: number; reassigned: number }[];
  teachers: { teacherId: number; gained: number; lost: number; moved: number }[];
  changes: LessonChange[];
};

export function useComparison(fromId: number | undefined, toId: number | undefined) {
  return useQuery({
    queryKey: ["timetable", "compare", fromId ?? 0, toId ?? 0],
    queryFn: () => apiRequest<Comparison>(`/api/v1/timetables/${fromId}/compare/${toId}`),
    enabled: fromId !== undefined && toId !== undefined,
  });
}

export type Violation = {
  code: string;
  rule: string;
  sectionId: number | null;
  teacherId: number | null;
  subjectId: number | null;
  resourceId: number | null;
  day: number | null;
  lesson: number | null;
  count: number | null;
  limit: number | null;
};

export type TimetableCheck = { violations: Violation[]; score: { total: number } };

export function checkTimetable(id: number, lessons: GridLesson[]) {
  return apiRequest<TimetableCheck>(`/api/v1/timetables/${id}/check`, "POST", { lessons, note: null });
}

export function useSaveEdit() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, lessons, note }: { id: number; lessons: GridLesson[]; note: string | null }) =>
      apiRequest<TimetableVersionSummary>(`/api/v1/timetables/${id}/edits`, "POST", { lessons, note }),
    onSuccess: () => refreshLifecycle(client),
  });
}

/** The session a working day falls in during a semester (morning when unmapped). */
export function sessionOn(sessions: GridSessions, term: Term, day: number): SessionName {
  return sessions.days.find((item) => item.term === term && item.day === day)?.session ?? "morning";
}

/** "HH:mm" for the formatter, from minutes after midnight. */
export function clock(minutes: number): string {
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`;
}
