import { CalendarOff, Clock, Maximize, Moon, Sun, WifiOff, X } from "lucide-react";
import { useEffect, useState } from "react";
import { ApiRequestError } from "../../i18n/errors";
import { messages } from "../../i18n/messages";
import { formatNumber } from "../../lib/format";
import { formatTime12 } from "../../lib/time";
import { Button } from "../../components/ui/button";
import { KioskBoard } from "../../components/ui/kiosk-board";
import { subjectColorClasses, type SubjectColorIndex } from "../../components/ui/timetable-cell";
import { clockOf, kioskKey, lessonOf, minuteOfDay, momentAt, type KioskData, type KioskLesson, type KioskMoment, type KioskPeriod } from "./kiosk";

const text = messages.school.kioskScreen;
const refreshMs = 60_000;
const tickMs = 15_000;

type Theme = "light" | "dark";

function storedTheme(): Theme | null {
  try {
    const value = window.localStorage.getItem("kiosk-theme");
    return value === "light" || value === "dark" ? value : null;
  } catch {
    return null;
  }
}

function sessionStore(): Storage | null {
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

async function fetchToday(key: string): Promise<KioskData> {
  let response: Response;
  try {
    response = await fetch("/api/v1/kiosk/today", { headers: { "X-Kiosk-Key": key }, cache: "no-store", credentials: "omit", signal: AbortSignal.timeout(10_000) });
  } catch {
    throw new ApiRequestError("NETWORK_ERROR");
  }
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { code?: string } | null;
    throw new ApiRequestError(payload?.code ?? "UNKNOWN_ERROR");
  }
  return await response.json() as KioskData;
}

/**
 * «شاشة العرض» (ADR 0056): the staff-room screen. Opened from «مشاركة الجدول مع الهاتف»; it reads today's timetable every minute
 * with its own key (never the owner's session) and needs no interaction. The current and next period come from this screen's clock in
 * the school's time zone.
 */
export function KioskPage() {
  const [key] = useState(() => kioskKey(window.location.hash, sessionStore()));
  const [data, setData] = useState<KioskData | null>(null);
  const [error, setError] = useState<ApiRequestError | null>(key ? null : new ApiRequestError("KIOSK_KEY_INVALID"));
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);
  const [now, setNow] = useState(() => new Date());
  const [theme, setTheme] = useState<Theme>(() => storedTheme() ?? (window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light"));

  useEffect(() => {
    document.title = text.documentTitle;
    // The key stays in this tab only; the address bar no longer shows it.
    if (window.location.hash) window.history.replaceState(null, "", window.location.pathname);
  }, []);

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    try {
      window.localStorage.setItem("kiosk-theme", theme);
    } catch {
      // Blocked storage: the theme applies for this visit.
    }
  }, [theme]);

  useEffect(() => {
    if (!key) return undefined;
    let stopped = false;
    const load = async () => {
      try {
        const today = await fetchToday(key);
        if (stopped) return;
        setData(today);
        setError(null);
        setUpdatedAt(new Date());
      } catch (reason) {
        if (!stopped) setError(reason instanceof ApiRequestError ? reason : new ApiRequestError("UNKNOWN_ERROR"));
      }
    };
    void load();
    const refresh = window.setInterval(() => void load(), refreshMs);
    return () => {
      stopped = true;
      window.clearInterval(refresh);
    };
  }, [key]);

  useEffect(() => {
    const tick = window.setInterval(() => setNow(new Date()), tickMs);
    return () => window.clearInterval(tick);
  }, []);

  const keyGone = error?.code === "KIOSK_KEY_INVALID";
  const minute = data ? minuteOfDay(now, data.timeZone) : 0;
  const moment = data ? momentAt(data, minute) : null;
  const time = (value: number) => formatTime12(clockOf(value));
  const canFullscreen = typeof document.documentElement.requestFullscreen === "function";

  return (
    <div className="kiosk" data-theme-kiosk={theme}>
      <header className="kiosk-header">
        <div className="kiosk-titles">
          <h1 className="kiosk-school">{data?.schoolName || text.documentTitle}</h1>
          {data && <p className="kiosk-meta">{[data.versionNumber ? text.version(formatNumber(data.versionNumber)) : null, updatedAt ? text.updated(formatTime12(clockOf(minuteOfDay(updatedAt, data.timeZone)))) : null].filter(Boolean).join(text.separator)}</p>}
        </div>
        <p className="kiosk-clock" aria-live="off"><Clock aria-hidden="true" size={40} /><span>{data ? time(minute) : ""}</span></p>
        <div className="kiosk-controls">
          {canFullscreen && (
            <Button variant="secondary" icon={<Maximize aria-hidden="true" size={20} />} onClick={() => void document.documentElement.requestFullscreen().catch(() => undefined)}>
              {text.fullscreen}
            </Button>
          )}
          <Button variant="secondary" aria-pressed={theme === "dark"} icon={theme === "dark" ? <Sun aria-hidden="true" size={20} /> : <Moon aria-hidden="true" size={20} />}
            onClick={() => setTheme(theme === "dark" ? "light" : "dark")}>
            {theme === "dark" ? text.themeLight : text.themeDark}
          </Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={() => window.close()}>{text.close}</Button>
        </div>
      </header>

      {error && !keyGone && data && <p className="kiosk-notice" role="status"><WifiOff aria-hidden="true" size={28} /><span>{text.offline}</span></p>}
      {keyGone && <p className="kiosk-banner" role="alert"><CalendarOff aria-hidden="true" size={56} /><span>{messages.errors.KIOSK_KEY_INVALID}</span></p>}
      {!keyGone && error && !data && <p className="kiosk-banner" role="alert"><WifiOff aria-hidden="true" size={56} /><span>{error.arabicMessage}</span></p>}
      {!error && !data && <p className="kiosk-banner" role="status"><Clock aria-hidden="true" size={56} /><span>{text.loading}</span></p>}
      {data && moment && !keyGone && <KioskBody data={data} moment={moment} time={time} />}
    </div>
  );
}

function KioskBody({ data, moment, time }: { data: KioskData; moment: KioskMoment; time: (minute: number) => string }) {
  if (moment.kind === "closed") {
    const message = moment.status === "dayOff" ? text.dayOff(moment.title ?? "") : moment.status === "notAWorkingDay" ? text.notAWorkingDay
      : moment.status === "outsideYear" ? text.outsideYear : text.noTimetable;
    return <p className="kiosk-banner" role="status"><CalendarOff aria-hidden="true" size={56} /><span>{message}</span></p>;
  }
  if (moment.kind === "dayOver") {
    return <p className="kiosk-banner" role="status"><Clock aria-hidden="true" size={56} /><span>{text.withReason(text.outsideHours, text.dayOver)}</span></p>;
  }
  const current = moment.kind === "open" ? moment.current : null;
  const next = moment.next;
  const periodTitle = (period: KioskPeriod) => period.number === null ? text.break : text.lesson(formatNumber(period.number));
  return (
    <main className="kiosk-main">
      {moment.kind === "beforeStart" && (
        <p className="kiosk-notice" role="status"><Clock aria-hidden="true" size={28} /><span>{text.withReason(text.outsideHours, text.beforeStart(time(moment.startMinute)))}</span></p>
      )}
      <KioskBoard
        sectionHeader={text.section}
        nowHeader={<><span className="kiosk-column-title">{text.now}</span>{current && <span className="kiosk-column-period">{text.periodAt(periodTitle(current), text.until(time(current.endMinute)))}</span>}</>}
        nextHeader={<><span className="kiosk-column-title">{text.next}</span>{next && <span className="kiosk-column-period">{text.periodAt(periodTitle(next), text.from(time(next.startMinute)))}</span>}</>}
        rows={data.sections.map((section) => ({
          key: section.index,
          label: text.sectionName(section.stageName, section.label),
          now: current ? current.number === null ? <span className="kiosk-break">{text.break}</span> : <Cell lesson={lessonOf(data, section.index, current.number)} /> : <span className="kiosk-free">{text.free}</span>,
          next: next ? <Cell lesson={lessonOf(data, section.index, next.number)} /> : <span className="kiosk-free">{text.free}</span>,
        }))}
      />
    </main>
  );
}

function Cell({ lesson }: { lesson: KioskLesson | undefined }) {
  if (!lesson) return <span className="kiosk-free">{text.free}</span>;
  return (
    <span className={`kiosk-lesson ${subjectColorClasses[(lesson.colorIndex in subjectColorClasses ? lesson.colorIndex : 8) as SubjectColorIndex]}`}>
      <span className="kiosk-subject">{lesson.subject}</span>
      <span className="kiosk-teacher">{lesson.teacher}</span>
    </span>
  );
}
