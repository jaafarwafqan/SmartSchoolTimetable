import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";

/** Every solver diagnostic code (mirrors the backend DiagnosticCodes.All; checked by GenerationCodeContractTests). */
export const diagnosticCodes = [
  "CORE_TEACHER_AVAILABILITY",
  "CORE_TEACHER_LIMITS",
  "CORE_SECTION_PACKING",
  "CORE_STAGE_DAYS",
  "CORE_SUBJECT_BLOCKED",
  "CORE_RESOURCE_CAPACITY",
  "CORE_SUBJECT_DAILY_CAP",
  "CORE_DOUBLE_PERIODS",
  "CORE_LOCKED_LESSONS",
  "CORE_FUNDAMENTAL",
  "TIMEOUT_NO_SOLUTION",
] as const;
export type DiagnosticCode = (typeof diagnosticCodes)[number];

export type GenerationMode = "standard" | "doublePeriods";
export type RunStatus = "queued" | "validating" | "generating" | "completed" | "cancelled" | "failed" | "infeasible" | "timedOut" | "interrupted";
export type EntityRef = { kind: string; id: number; name: string };

export type SolverFinding = {
  code: string;
  entity: EntityRef;
  related: EntityRef[];
  required: number | null;
  available: number | null;
  fixes: string[];
  /** True: relaxing only this makes a timetable possible; null: not tested within the budget. */
  relaxationHelps: boolean | null;
};

export type RuleScore = { key: string; enabled: boolean; weight: number; penalty: number; weighted: number };

/** Real counters of an active run (solver events only, never an estimate). */
export type LiveProgress = {
  phase: "queued" | "validating" | "generating" | "saving";
  elapsedSeconds: number | null;
  improvements: number;
  bestObjective: number | null;
  bestBound: number | null;
  firstSolutionSeconds: number | null;
};

export type GenerationRun = {
  id: number;
  academicYearId: number;
  status: RunStatus;
  mode: GenerationMode;
  timeLimitSeconds: number;
  seed: number;
  workers: number;
  deterministic: boolean;
  solverVersion: string;
  inputHash: string;
  profileVersion: number;
  queuedAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  elapsedSeconds: number | null;
  objective: number | null;
  bound: number | null;
  optimal: boolean;
  improvements: number;
  firstSolutionSeconds: number | null;
  lessonsPlaced: number;
  score: { total: number; rules: RuleScore[] } | null;
  diagnostics: { findings: SolverFinding[]; minimal: boolean } | null;
  errorCode: string | null;
  timetableVersionId: number | null;
  live: LiveProgress | null;
  /** «إبقاء تعديلاتي»: the version whose manual lessons were locked, how many, and how many could not be kept. */
  lockedFromVersionId: number | null;
  lockedLessons: number;
  locksDropped: number;
};

export type EngineStatus = {
  available: boolean;
  version: string | null;
  defaultWorkers: number;
  maxWorkers: number;
  defaultTimeLimit: number;
  minTimeLimit: number;
  maxTimeLimit: number;
};

export type StartGeneration = {
  mode: GenerationMode;
  timeLimitSeconds: number;
  deterministic: boolean;
  seed: number | null;
  workers: number;
  /** Keep this version's manual edits as locked lessons; omit to discard them. */
  lockFromVersionId?: number;
};

/** The latest version of the year when it is a manual edit, and how many lessons were moved by hand. */
export type ManualEdits = { versionId: number; number: number; lessons: number };

const activeStatuses: readonly RunStatus[] = ["queued", "validating", "generating"];

export function isActive(run: GenerationRun | null | undefined): boolean {
  return run !== null && run !== undefined && activeStatuses.includes(run.status);
}

/** Polling interval while a run is active (Phase 4: progress is polled, no push channel). */
export const pollMilliseconds = 1000;

export const generationKeys = {
  engine: ["generation", "engine"] as const,
  current: (yearId: number) => ["generation", "current", yearId] as const,
  history: (yearId: number) => ["generation", "history", yearId] as const,
  manualEdits: (yearId: number) => ["generation", "manual-edits", yearId] as const,
};

export function useManualEdits(yearId: number | undefined) {
  return useQuery({
    queryKey: generationKeys.manualEdits(yearId ?? 0),
    queryFn: () => apiRequest<{ edits: ManualEdits | null }>(`/api/v1/academic-years/${yearId}/generation/manual-edits`),
    enabled: yearId !== undefined,
  });
}

export function useEngine() {
  return useQuery({ queryKey: generationKeys.engine, queryFn: () => apiRequest<EngineStatus>("/api/v1/generation/engine"), staleTime: Infinity });
}

/** The latest run of the year; polled every second while it is active, so a reopened page resumes the progress. */
export function useCurrentRun(yearId: number | undefined) {
  return useQuery({
    queryKey: generationKeys.current(yearId ?? 0),
    queryFn: () => apiRequest<{ run: GenerationRun | null }>(`/api/v1/academic-years/${yearId}/generation/current`),
    enabled: yearId !== undefined,
    refetchInterval: (query) => (isActive(query.state.data?.run) ? pollMilliseconds : false),
  });
}

export function useRunHistory(yearId: number | undefined) {
  return useQuery({
    queryKey: generationKeys.history(yearId ?? 0),
    queryFn: () => apiRequest<{ items: GenerationRun[]; total: number }>(`/api/v1/academic-years/${yearId}/generation/runs?pageSize=10`),
    enabled: yearId !== undefined,
  });
}

export function useStartGeneration(yearId: number | undefined) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (command: StartGeneration) => apiRequest<GenerationRun>(`/api/v1/academic-years/${yearId}/generation/runs`, "POST", command),
    onSuccess: (run) => {
      client.setQueryData(generationKeys.current(run.academicYearId), { run });
      void client.invalidateQueries({ queryKey: ["generation", "history"] });
    },
  });
}

export function useCancelGeneration() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (runId: number) => apiRequest<GenerationRun>(`/api/v1/generation/runs/${runId}/cancel`, "POST"),
    onSuccess: () => void client.invalidateQueries({ queryKey: ["generation"] }),
  });
}
