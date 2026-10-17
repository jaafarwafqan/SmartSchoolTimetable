/** `GET /api/v1/kiosk/today` (ADR 0056): today's clock and every section's lessons of the approved version. */
export type KioskPeriod = { number: number | null; startMinute: number; endMinute: number };
export type KioskSection = { index: number; stageName: string; label: string };
export type KioskLesson = { section: number; lesson: number; subject: string; colorIndex: number; teacher: string };
export type KioskStatus = "school" | "dayOff" | "notAWorkingDay" | "outsideYear" | "noTimetable";
export type KioskData = {
  schoolName: string; date: string; timeZone: string; status: KioskStatus; dayOffTitle: string | null; versionNumber: number | null;
  semester: number; session: number; periods: KioskPeriod[]; sections: KioskSection[]; lessons: KioskLesson[];
};

/** What the screen shows at one minute of the day. */
export type KioskMoment =
  | { kind: "closed"; status: Exclude<KioskStatus, "school">; title: string | null }
  | { kind: "beforeStart"; startMinute: number; next: KioskPeriod }
  | { kind: "dayOver" }
  /** `current` is a lesson or a break; `next` is the next LESSON (null after the last one). */
  | { kind: "open"; current: KioskPeriod; next: KioskPeriod | null };

export function momentAt(data: KioskData, minute: number): KioskMoment {
  if (data.status !== "school") return { kind: "closed", status: data.status, title: data.dayOffTitle };
  const lessons = data.periods.filter((period) => period.number !== null);
  if (lessons.length === 0) return { kind: "closed", status: "notAWorkingDay", title: null };
  const first = data.periods[0];
  const last = data.periods[data.periods.length - 1];
  if (minute < first.startMinute) return { kind: "beforeStart", startMinute: first.startMinute, next: lessons[0] };
  if (minute >= last.endMinute) return { kind: "dayOver" };
  // Between two rows without a break row (a gap in the clock): the coming row is shown as current so the screen is never empty.
  const current = data.periods.find((period) => period.startMinute <= minute && minute < period.endMinute)
    ?? data.periods.find((period) => period.startMinute > minute)!;
  const next = lessons.find((period) => period.startMinute > current.startMinute) ?? null;
  return { kind: "open", current, next };
}

/** The lesson a section has in lesson number `number` today (undefined: a free period). */
export function lessonOf(data: KioskData, section: number, number: number | null): KioskLesson | undefined {
  if (number === null) return undefined;
  return data.lessons.find((lesson) => lesson.section === section && lesson.lesson === number);
}

/** Minutes after midnight of `now` in the school's time zone (the screen's clock may be set to another zone). */
export function minuteOfDay(now: Date, timeZone: string): number {
  const parts = new Intl.DateTimeFormat("en-GB", { timeZone, hour: "2-digit", minute: "2-digit", hourCycle: "h23" }).formatToParts(now);
  const value = (type: string) => Number(parts.find((part) => part.type === type)?.value ?? "0");
  return value("hour") * 60 + value("minute");
}

/** "HH:mm" (24-hour) for the shared 12-hour formatter. */
export function clockOf(minute: number): string {
  return `${String(Math.floor(minute / 60)).padStart(2, "0")}:${String(minute % 60).padStart(2, "0")}`;
}

/** The kiosk key travels in the address fragment (never sent to the server in the URL) and is kept for reloads of this tab. */
export function kioskKey(hash: string, storage: Pick<Storage, "getItem" | "setItem"> | null): string | null {
  const match = /(?:^#|&)k=([A-Za-z0-9_-]{16,64})/.exec(hash);
  if (match) {
    try {
      storage?.setItem("kiosk-key", match[1]);
    } catch {
      // Storage may be blocked; the key in the address still works.
    }
    return match[1];
  }
  try {
    return storage?.getItem("kiosk-key") ?? null;
  } catch {
    return null;
  }
}
