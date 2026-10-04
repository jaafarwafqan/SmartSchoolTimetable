import { messages } from "../../i18n/messages";

/** Maps server checklist keys to their Arabic label and the screen that completes the step. */
export const checklistSteps: Record<string, { label: string; to: string }> = {
  schoolProfile: { label: messages.school.dashboard.steps.schoolProfile, to: "/school" },
  academicYear: { label: messages.school.dashboard.steps.academicYear, to: "/academic-years" },
  timetableStructure: { label: messages.school.dashboard.steps.timetableStructure, to: "/schedule-structure" },
  stagesSections: { label: messages.school.dashboard.steps.stagesSections, to: "/stages-sections" },
  subjects: { label: messages.school.dashboard.steps.subjects, to: "/subjects" },
};

/** Maps server count keys to their Arabic label and the screen that lists them. */
export const countItems: Record<string, { label: string; to: string }> = {
  academicYears: { label: messages.school.dashboard.counts.academicYears, to: "/academic-years" },
  stages: { label: messages.school.dashboard.counts.stages, to: "/stages-sections" },
  sections: { label: messages.school.dashboard.counts.sections, to: "/stages-sections" },
  subjects: { label: messages.school.dashboard.counts.subjects, to: "/subjects" },
  capacityGaps: { label: messages.school.dashboard.counts.capacityGaps, to: "/stages-sections" },
};
