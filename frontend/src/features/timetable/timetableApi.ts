import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";

export type TimetableVersionSummary = {
  id: number;
  version: number;
  number: number;
  source: "generated" | "edited";
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
};

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

export function useApproveTimetable() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (version: TimetableVersionSummary) => apiRequest<TimetableVersionSummary>(`/api/v1/timetables/${version.id}/approve`, "POST", { version: version.version }),
    onSuccess: () => void client.invalidateQueries({ queryKey: ["timetable"] }),
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
    onSuccess: () => void client.invalidateQueries({ queryKey: ["timetable"] }),
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
