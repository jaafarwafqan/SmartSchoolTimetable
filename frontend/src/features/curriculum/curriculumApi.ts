import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { refreshQueries } from "../../lib/refreshQueries";
import { useRefreshSchoolData } from "../../lib/schoolContext";
import type { Section, Stage } from "../stages-sections/stagesApi";

export type CapacityStatus = "under" | "equal" | "over";
export type ShiftTotal = { shiftId: number; shiftName: string; sections: number; weeklyCapacity: number; status: CapacityStatus; difference: number };
export type CurriculumStage = { id: number; name: string; plannedLessons: number; totals: ShiftTotal[] };
/** `isSuggested`: the value came from the suggested template and was not edited since («مقترح»). */
export type CurriculumCell = { stageId: number; entryId: number | null; weeklyLessons: number | null; version: number | null; duplicates: number; isSuggested: boolean };
export type CurriculumRow = { subjectId: number; subjectName: string; colorIndex: number; label: string | null; cells: CurriculumCell[] };
export type CurriculumEntry = { id: number; stageId: number; subjectId: number; weeklyLessons: number; label: string | null; isArchived: boolean; version: number };
/** `cleared`: the line a cell edit just cleared (archived), for the undo notice (Phase 3 §5.4). */
export type CurriculumTable = { stages: CurriculumStage[]; rows: CurriculumRow[]; cleared?: CurriculumEntry | null };
export type CellInput = { stageId: number; subjectId: number; label: string | null; weeklyLessons: number | null; entryId: number | null; version: number | null };

export type PlanAction = "create" | "exists" | "notApplicable" | "update" | "unchanged" | "ambiguous";
export type CurriculumPlanLine = { stageId: number; stageName: string; subjectId: number; subjectName: string; label: string | null; weeklyLessons: number; action: PlanAction };
export type CurriculumPlan = { lines: CurriculumPlanLine[]; changes: number };
export type CopyInput = { fromStageId: number; toStageIds: number[] };
export type AcrossInput = { subjectId: number; label: string | null; weeklyLessons: number; stageIds: number[] };

export type StageCard = { stage: Stage; sections: Section[] };
export type LabelStyle = "arabic" | "numbers" | "latin";
export type SectionCountInput = { stageId: number; count: number; shiftId: number; labelStyle: LabelStyle };

export type BranchTemplate = { key: string; name: string };
export type GradeTemplate = { key: string; name: string; branchStem: string | null; schoolTypes: string[] };
export type BreakSlot = { afterLesson: number; minutes: number };
export type PeriodPreset = { key: string; name: string; firstStart: string; lessonMinutes: number; lessonCount: number; breaks: BreakSlot[] };
export type WorkingDayPreset = { key: string; name: string; days: number[]; weekStart: number; isDefault: boolean };
/** Suggested break length per school type (minutes); a suggestion only (ADR 0026). */
export type BreakDefaults = { minutes: Record<string, number> };
export type TemplateCatalog = { branches: BranchTemplate[]; grades: GradeTemplate[]; periodPresets: PeriodPreset[]; workingDayPresets: WorkingDayPreset[]; breakDefaults: BreakDefaults };
export type StageTemplateGrade = { gradeKey: string; branches: string[]; sections: number; shiftId: number | null; labelStyle: LabelStyle };
export type StageTemplateInput = { schoolType: string; grades: StageTemplateGrade[] };
export type StagePlanLine = { key: string; name: string; action: PlanAction; existingSections: number; sectionsToAdd: number };
export type StagePlan = { lines: StagePlanLine[]; changes: number };
export type SubjectPlan = { lines: { name: string; action: PlanAction }[]; changes: number };

export const curriculumKey = ["curriculum"] as const;
export const stageCardsKey = ["stage-cards"] as const;
const yearPath = (yearId: number) => `/api/v1/academic-years/${yearId}`;

export function useCurriculum(yearId: number | null) {
  return useQuery({
    queryKey: [...curriculumKey, yearId],
    enabled: yearId !== null,
    queryFn: () => apiRequest<CurriculumTable>(`${yearPath(yearId ?? 0)}/curriculum`),
  });
}

export function useStageCards(yearId: number | null) {
  return useQuery({
    queryKey: [...stageCardsKey, yearId],
    enabled: yearId !== null,
    queryFn: () => apiRequest<StageCard[]>(`${yearPath(yearId ?? 0)}/stage-cards`),
  });
}

export function useTemplateCatalog() {
  return useQuery({ queryKey: ["templates"], queryFn: () => apiRequest<TemplateCatalog>("/api/v1/templates/"), staleTime: Infinity });
}

export function useSuggestedSubjects(yearId: number | null) {
  return useQuery({
    queryKey: ["suggested-subjects", yearId],
    enabled: yearId !== null,
    queryFn: () => apiRequest<string[]>(`${yearPath(yearId ?? 0)}/templates/suggested-subjects`),
  });
}

/** Any structure change refreshes the stage, section, subject and curriculum views and the dashboard. */
function useSetupMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([refreshQueries(queryClient, [curriculumKey, stageCardsKey, ["stages"], ["sections"], ["subjects"], ["suggested-subjects"], ["daily-suggestion"]]), refreshSchoolData()]);
    },
  });
}

export function useSetCell(yearId: number) {
  return useSetupMutation((input: CellInput) => apiRequest<CurriculumTable>(`${yearPath(yearId)}/curriculum/cell`, "PUT", input));
}

/** Undo of a cleared cell: restores the archived line with the version the clear returned. */
export function useRestoreEntry() {
  return useSetupMutation((entry: CurriculumEntry) => apiRequest<CurriculumEntry>(`/api/v1/curriculum-entries/${entry.id}/restore`, "POST", { version: entry.version }));
}

/** Preview (no change) or apply; previews skip the refresh because nothing was saved. */
function usePlan<TInput, TPlan>(path: (input: TInput) => string) {
  const apply = useSetupMutation((input: TInput) => apiRequest<TPlan>(path(input), "POST", input));
  const preview = useMutation({ mutationFn: (input: TInput) => apiRequest<TPlan>(`${path(input)}/preview`, "POST", input) });
  return { apply, preview };
}

export function useCopyCurriculum(yearId: number) {
  return usePlan<CopyInput, CurriculumPlan>(() => `${yearPath(yearId)}/curriculum/copy`);
}

export function useSetAcross(yearId: number) {
  return usePlan<AcrossInput, CurriculumPlan>(() => `${yearPath(yearId)}/curriculum/set-across`);
}

export function useStageTemplate(yearId: number) {
  return usePlan<StageTemplateInput, StagePlan>(() => `${yearPath(yearId)}/templates/stages`);
}

export function useSubjectTemplate() {
  return usePlan<{ names: string[] }, SubjectPlan>(() => "/api/v1/templates/subjects");
}

export function useSetStageDayLessons(yearId: number) {
  return useSetupMutation(({ stageId, dayLessons, version }: { stageId: number; dayLessons: { day: number; lessons: number }[]; version: number }) =>
    apiRequest<Stage>(`${yearPath(yearId)}/stages/${stageId}/day-lessons`, "PUT", { dayLessons, version }));
}

export function useSetSectionCount(yearId: number) {
  return useSetupMutation(({ stageId, ...input }: SectionCountInput) =>
    apiRequest<StageCard>(`${yearPath(yearId)}/stage-cards/${stageId}/section-count`, "PUT", input));
}

// Suggested Iraqi curriculum (ADR 0028, 0029) and the daily distribution (ADR 0030).
export type SuggestedEntryLine = { subject: string; lessons: number; action: "create" | "exists" | "update" | "unchanged" | "skipped"; optional: boolean; currentLessons: number | null };
export type SuggestedStage = { stageId: number; stageName: string; needsReview: boolean; statedTotal: number; suggestedTotal: number; currentTotal: number; resultingTotal: number; entries: SuggestedEntryLine[] };
export type SuggestedSubjectLine = { name: string; action: "create" | "exists"; existingName: string | null; optional: boolean; included: boolean };
export type SuggestedPlan = { provenance: string; subjects: SuggestedSubjectLine[]; stages: SuggestedStage[]; optionalSubjects: string[]; changes: number };
export type SuggestedInput = { optionalSubjects: string[]; confirm?: boolean };
export type DailyStatus = "apply" | "same" | "manual" | "aboveCapacity" | "belowDays" | "noCurriculum";
export type DailyStage = { stageId: number; stageName: string; weeklyTotal: number; capacity: number; suggested: DayLessonsEntry[]; current: DayLessonsEntry[]; status: DailyStatus; changedSinceSuggestion: boolean; version: number };
type DayLessonsEntry = { day: number; lessons: number };

const suggestedPath = (yearId: number) => `${yearPath(yearId)}/curriculum/suggested`;

/** The suggestion for the current choices; also tells which stages need review (used by the table header). */
export function useSuggestedPreview(yearId: number, optionalSubjects: readonly string[]) {
  return useQuery({
    queryKey: [...curriculumKey, "suggested", yearId, [...optionalSubjects].sort()],
    queryFn: () => apiRequest<SuggestedPlan>(`${suggestedPath(yearId)}/preview`, "POST", { optionalSubjects }),
  });
}

export function useApplySuggested(yearId: number) {
  return useSetupMutation((input: SuggestedInput) => apiRequest<SuggestedPlan>(suggestedPath(yearId), "POST", input));
}

export function usePreviewStageReset(yearId: number) {
  return useMutation({
    mutationFn: ({ stageId, ...input }: SuggestedInput & { stageId: number }) =>
      apiRequest<SuggestedPlan>(`${suggestedPath(yearId)}/stages/${stageId}/reset/preview`, "POST", input),
  });
}

export function useResetStage(yearId: number) {
  return useSetupMutation(({ stageId, ...input }: SuggestedInput & { stageId: number }) =>
    apiRequest<SuggestedPlan>(`${suggestedPath(yearId)}/stages/${stageId}/reset`, "POST", { ...input, confirm: true }));
}

export const dailyKey = ["daily-suggestion"] as const;

export function useDailySuggestion(yearId: number | null) {
  return useQuery({
    queryKey: [...dailyKey, yearId],
    enabled: yearId !== null,
    queryFn: () => apiRequest<{ stages: DailyStage[] }>(`${yearPath(yearId ?? 0)}/daily-suggestion`),
  });
}

export function useApplyDailySuggestion(yearId: number) {
  return useSetupMutation((stageIds: number[]) => apiRequest<{ stages: DailyStage[] }>(`${yearPath(yearId)}/daily-suggestion`, "POST", { stageIds }));
}
