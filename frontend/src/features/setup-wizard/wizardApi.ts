import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import type { ShiftSystemCommand } from "../timetable-structure/shiftSystem";
import type { BreakSlot } from "../curriculum/curriculumApi";
import type { DayLessons } from "../timetable-structure/scheduleApi";

export type SetupProgress = {
  currentStep: number;
  completedSteps: number[];
  skippedSteps: number[];
  isFinished: boolean;
  schoolType: string;
  shiftMode: "morning" | "evening" | "dual";
  version: number;
};

export type WizardSchoolInput = { name: string; schoolType: string; principalName: string | null };
export type WizardYearInput = { label: string; startDate: string; endDate: string; terms: { name: string; startDate: string; endDate: string }[] };
export type WizardShiftInput = {
  kind: "morning" | "evening";
  firstStartTime: string;
  lessonMinutes: number;
  lessonCount: number;
  breaks: BreakSlot[];
  gapMinutes: number;
  dayLessons: DayLessons[];
};
/** MF7: the timing step saves the whole system of work (one shift; «مزدوج» adds the evening session and the day mapping). */
export type WizardTimingInput = { days: number[]; weekStartDay: number } & ShiftSystemCommand;
export type SetupWarning = { code: "noSections" | "emptyCurriculum" | "under" | "over"; stageName: string; shiftName: string | null; value: number };
export type SetupReview = {
  schoolName: string;
  yearLabel: string | null;
  shifts: number;
  stages: number;
  sections: number;
  subjects: number;
  curriculumLines: number;
  teachers: number;
  warnings: SetupWarning[];
};

export const wizardStepCount = 8;
export const setupProgressKey = ["setup-progress"] as const;

export function useSetupProgress() {
  return useQuery({ queryKey: setupProgressKey, queryFn: () => apiRequest<SetupProgress>("/api/v1/setup-progress/") });
}

export function useSetupReview(enabled: boolean) {
  return useQuery({ queryKey: ["setup-review"], enabled, queryFn: () => apiRequest<SetupReview>("/api/v1/setup-wizard/review") });
}

/** A wizard step can change any screen's data, so every cached query is refreshed after it. */
function useWizardMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  // Cancel first: an in-flight first load would otherwise be reused and land with stale data (see refreshQueries).
  return useMutation({ mutationFn: request, onSuccess: async () => { await queryClient.cancelQueries(); await queryClient.invalidateQueries(); } });
}

export function useSaveSchoolStep() {
  return useWizardMutation((input: WizardSchoolInput) => apiRequest<SetupProgress>("/api/v1/setup-wizard/school", "PUT", input));
}

export function useSaveYearStep() {
  return useWizardMutation((input: WizardYearInput) => apiRequest<SetupProgress>("/api/v1/setup-wizard/year", "PUT", input));
}

export function useSaveTimingStep() {
  return useWizardMutation((input: WizardTimingInput) => apiRequest<SetupProgress>("/api/v1/setup-wizard/timing", "PUT", input));
}

/** Records a step done or skipped (steps 4–7 save their data through their own screens' endpoints). */
export function useRecordStep() {
  return useWizardMutation(({ progress, step, skipped = false, finish = false }: { progress: SetupProgress; step: number; skipped?: boolean; finish?: boolean }) =>
    apiRequest<SetupProgress>("/api/v1/setup-progress/", "PUT", {
      currentStep: Math.min(wizardStepCount, Math.max(progress.currentStep, step + 1)),
      completedSteps: skipped ? progress.completedSteps : [...new Set([...progress.completedSteps, step])],
      skippedSteps: skipped ? [...new Set([...progress.skippedSteps, step])] : progress.skippedSteps,
      isFinished: finish || progress.isFinished,
      version: progress.version,
    }));
}
