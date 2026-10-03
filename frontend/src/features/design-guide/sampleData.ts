import type { SubjectColorIndex } from "../../components/ui/timetable-cell";

// Sample data for the development-only style guide. Not used by any product screen.

export type ColorToken = { name: string; swatchClass: string };

// Literal class names so Tailwind generates a utility for every token.
export const colorTokens: readonly ColorToken[] = [
  { name: "canvas", swatchClass: "bg-canvas" },
  { name: "surface", swatchClass: "bg-surface" },
  { name: "surface-muted", swatchClass: "bg-surface-muted" },
  { name: "line", swatchClass: "bg-line" },
  { name: "line-strong", swatchClass: "bg-line-strong" },
  { name: "ink", swatchClass: "bg-ink" },
  { name: "ink-muted", swatchClass: "bg-ink-muted" },
  { name: "ink-subtle", swatchClass: "bg-ink-subtle" },
  { name: "primary", swatchClass: "bg-primary" },
  { name: "primary-hover", swatchClass: "bg-primary-hover" },
  { name: "primary-soft", swatchClass: "bg-primary-soft" },
  { name: "primary-ink", swatchClass: "bg-primary-ink" },
  { name: "on-primary", swatchClass: "bg-on-primary" },
  { name: "danger", swatchClass: "bg-danger" },
  { name: "danger-soft", swatchClass: "bg-danger-soft" },
  { name: "success", swatchClass: "bg-success" },
  { name: "success-soft", swatchClass: "bg-success-soft" },
  { name: "warning", swatchClass: "bg-warning" },
  { name: "warning-soft", swatchClass: "bg-warning-soft" },
  { name: "info", swatchClass: "bg-info" },
  { name: "info-soft", swatchClass: "bg-info-soft" },
  { name: "subject-1", swatchClass: "bg-subject-1" },
  { name: "subject-2", swatchClass: "bg-subject-2" },
  { name: "subject-3", swatchClass: "bg-subject-3" },
  { name: "subject-4", swatchClass: "bg-subject-4" },
  { name: "subject-5", swatchClass: "bg-subject-5" },
  { name: "subject-6", swatchClass: "bg-subject-6" },
  { name: "subject-7", swatchClass: "bg-subject-7" },
  { name: "subject-8", swatchClass: "bg-subject-8" },
  { name: "subject-9", swatchClass: "bg-subject-9" },
  { name: "subject-10", swatchClass: "bg-subject-10" },
  { name: "on-subject", swatchClass: "bg-on-subject" },
];

export type SampleTeacher = { id: string; name: string; subject: string; lessons: number };

export const sampleTeachers: readonly SampleTeacher[] = [
  { id: "1", name: "أحمد كريم", subject: "الرياضيات", lessons: 18 },
  { id: "2", name: "زينب علي", subject: "اللغة العربية", lessons: 20 },
  { id: "3", name: "حسين جاسم", subject: "الفيزياء", lessons: 16 },
  { id: "4", name: "مريم حسن", subject: "الكيمياء", lessons: 14 },
];

export type SampleSubject = { name: string; teacher: string; color: SubjectColorIndex };

export const sampleSubjects: readonly SampleSubject[] = [
  { name: "الرياضيات", teacher: "أ. أحمد", color: 1 },
  { name: "اللغة العربية", teacher: "أ. زينب", color: 2 },
  { name: "التربية الإسلامية", teacher: "أ. علي", color: 3 },
  { name: "اللغة الإنكليزية", teacher: "أ. سارة", color: 4 },
  { name: "الفيزياء", teacher: "أ. حسين", color: 5 },
  { name: "الكيمياء", teacher: "أ. مريم", color: 6 },
  { name: "الأحياء", teacher: "أ. نور", color: 7 },
  { name: "الحاسوب", teacher: "أ. عمار", color: 8 },
  { name: "الاجتماعيات", teacher: "أ. هدى", color: 9 },
  { name: "التربية الفنية", teacher: "أ. رنا", color: 10 },
];
