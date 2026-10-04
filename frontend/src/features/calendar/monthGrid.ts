/** ISO weekday (1 = Monday … 7 = Sunday) of a "yyyy-MM-dd" date, computed in UTC so time zones never shift it. */
export function isoWeekday(date: string): number {
  const day = new Date(`${date}T00:00:00Z`).getUTCDay();
  return day === 0 ? 7 : day;
}

/** "yyyy-MM-01" of the month containing `date`, moved by `offset` months. */
export function monthStart(date: string, offset = 0): string {
  const [year, month] = date.split("-").map(Number);
  const value = new Date(Date.UTC(year, month - 1 + offset, 1));
  return value.toISOString().slice(0, 10);
}

/** Last day ("yyyy-MM-dd") of the month that starts at `start`. */
export function monthEnd(start: string): string {
  const [year, month] = start.split("-").map(Number);
  return new Date(Date.UTC(year, month, 0)).toISOString().slice(0, 10);
}

/**
 * The month as weeks of seven cells starting on the school's week start day; cells outside the month are null.
 */
export function monthGrid(start: string, weekStart: number): (string | null)[][] {
  const end = Number(monthEnd(start).slice(8));
  const lead = (isoWeekday(start) - weekStart + 7) % 7;
  const cells: (string | null)[] = Array.from({ length: lead }, () => null);
  for (let day = 1; day <= end; day++) cells.push(`${start.slice(0, 8)}${String(day).padStart(2, "0")}`);
  while (cells.length % 7 !== 0) cells.push(null);
  return Array.from({ length: cells.length / 7 }, (_, week) => cells.slice(week * 7, week * 7 + 7));
}
