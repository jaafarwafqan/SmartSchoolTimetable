/** Every hard-constraint violation code (mirrors the backend ViolationCodes.All; checked by GenerationCodeContractTests). */
export const violationCodes = [
  "UNKNOWN_LESSON",
  "WRONG_LESSON_COUNT",
  "SECTION_CONFLICT",
  "OUTSIDE_SECTION_DAY",
  "SECTION_GAP",
  "TEACHER_CONFLICT",
  "TEACHER_UNAVAILABLE",
  "SUBJECT_BLOCKED",
  "TEACHER_DAY_LIMIT",
  "TEACHER_WEEK_LIMIT",
  "RESOURCE_CAPACITY",
  "SUBJECT_DAILY_CAP",
  "DOUBLE_PERIOD_BROKEN",
] as const;
export type ViolationCode = (typeof violationCodes)[number];
