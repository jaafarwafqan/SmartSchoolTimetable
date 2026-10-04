import { messages } from "../../i18n/messages";

const names = messages.school.scheduleStructure.days;

/** ISO weekday numbers (1 = Monday … 7 = Sunday, DECISIONS_PENDING #3) to Arabic names. */
const labels: Record<number, string> = {
  1: names.monday,
  2: names.tuesday,
  3: names.wednesday,
  4: names.thursday,
  5: names.friday,
  6: names.saturday,
  7: names.sunday,
};

export function weekdayLabel(day: number): string {
  return labels[day] ?? "";
}

/** The seven ISO weekdays in display order, starting from the school's week start day. */
export function weekdaysFrom(weekStart: number): number[] {
  return Array.from({ length: 7 }, (_, offset) => ((weekStart - 1 + offset) % 7) + 1);
}
