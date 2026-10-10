import { messages } from "../../i18n/messages";

/** Maps server checklist keys to their Arabic label and the screen that completes the step. */
export const checklistSteps: Record<string, { label: string; to: string }> = {
  schoolProfile: { label: messages.school.dashboard.steps.schoolProfile, to: "/school/profile" },
  academicYear: { label: messages.school.dashboard.steps.academicYear, to: "/school/year" },
  timetableStructure: { label: messages.school.dashboard.steps.timetableStructure, to: "/school/timing" },
  stagesSections: { label: messages.school.dashboard.steps.stagesSections, to: "/classes/stages" },
  subjects: { label: messages.school.dashboard.steps.subjects, to: "/classes/subjects" },
  teachers: { label: messages.school.dashboard.steps.teachers, to: "/teachers" },
  workload: { label: messages.school.dashboard.steps.workload, to: "/teachers/workload" },
};

/** Maps server count keys to their Arabic label and the screen that lists them. */
export const countItems: Record<string, { label: string; to: string }> = {
  teachers: { label: messages.school.dashboard.counts.teachers, to: "/teachers" },
  academicYears: { label: messages.school.dashboard.counts.academicYears, to: "/school/year" },
  stages: { label: messages.school.dashboard.counts.stages, to: "/classes/stages" },
  sections: { label: messages.school.dashboard.counts.sections, to: "/classes/stages" },
  subjects: { label: messages.school.dashboard.counts.subjects, to: "/classes/subjects" },
  capacityGaps: { label: messages.school.dashboard.counts.capacityGaps, to: "/classes/stages" },
};
