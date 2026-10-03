import { useState } from "react";
import { BellRing, CalendarDays, Plus, Save, Volume2, WandSparkles } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Input } from "../../components/ui/input";
import { Select } from "../../components/ui/select";
import { PageHeader } from "../../layout/PageHeader";
import { messages } from "../../i18n/messages";
import { useAcademicYears } from "../academic-years/yearsApi";
import { useFormatter } from "../../lib/schoolContext";
import { generatePeriods, useBellSettings, useCreateShift, useSavePeriods, useShifts, useUpdateBell, useUpdateWeek, useWorkingWeek, type Period, type Shift } from "./scheduleApi";
import { previewTone } from "./tonePreview";

const text = messages.school.scheduleStructure;
const weekdays: readonly { day: number; label: string }[] = [
  { day: 1, label: text.days.monday }, { day: 2, label: text.days.tuesday }, { day: 3, label: text.days.wednesday },
  { day: 4, label: text.days.thursday }, { day: 5, label: text.days.friday }, { day: 6, label: text.days.saturday }, { day: 7, label: text.days.sunday },
];
const tones = [{ value: "Classic", label: text.tones.classic }, { value: "Chime", label: text.tones.chime }, { value: "Beeps", label: text.tones.beeps }, { value: "Soft", label: text.tones.soft }] as const;

export function ScheduleStructurePage() {
  const format = useFormatter();
  const years = useAcademicYears({ search: "", page: 1, pageSize: 100 });
  const [yearId, setYearId] = useState<number>();
  const selectedYear = yearId ?? years.data?.items.find((year) => year.isCurrent)?.id ?? years.data?.items[0]?.id;
  const shifts = useShifts(selectedYear);
  const week = useWorkingWeek();
  const bells = useBellSettings();
  const createShift = useCreateShift(selectedYear ?? 0);
  const updateWeek = useUpdateWeek();
  const updateBell = useUpdateBell();
  const [name, setName] = useState("");
  const [order, setOrder] = useState("1");
  const [draft, setDraft] = useState<Period[]>([]);
  const [activeShift, setActiveShift] = useState<number>();
  const [firstStartTime, setFirstStartTime] = useState("08:00");
  const [lessonMinutes, setLessonMinutes] = useState("45");
  const [lessonCount, setLessonCount] = useState("7");
  const [breakMinutes, setBreakMinutes] = useState("15");
  const [breakAfterLesson, setBreakAfterLesson] = useState("4");
  const [soundError, setSoundError] = useState(false);
  const selectedShift = shifts.data?.items.find((shift) => shift.id === activeShift) ?? shifts.data?.items[0];
  const savePeriods = useSavePeriods(selectedYear ?? 0, selectedShift ?? emptyShift);

  async function generate() {
    try {
      const result = await generatePeriods(selectedYear ?? 0, { firstStartTime, lessonMinutes: Number(lessonMinutes), lessonCount: Number(lessonCount), breakMinutes: Number(breakMinutes), breakAfterLesson: Number(breakAfterLesson) || null });
      setDraft(result.periods);
    } catch { setDraft([]); }
  }

  const days = week.data?.days ?? [];
  const pageError = years.isError || shifts.isError || week.isError || bells.isError;
  return (
    <section className="page-stack">
      <PageHeader title={text.title} description={text.description} />
      {pageError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      <Card>
        <div className="page-header-row"><h2>{text.workingDays}</h2><Button icon={<Save size={18} />} loading={updateWeek.isPending} onClick={() => week.data && updateWeek.mutate({ days, weekStartDay: week.data.weekStartDay, version: week.data.version })}>{text.save}</Button></div>
        <fieldset className="schedule-day-grid"><legend>{text.chooseDays}</legend>{weekdays.map(({ day, label }) => <Checkbox key={day} checked={days.includes(day)} disabled={!week.data} onChange={(event) => { const next = event.target.checked ? [...days, day] : days.filter((value) => value !== day); if (week.data) updateWeek.mutate({ days: next, weekStartDay: week.data.weekStartDay, version: week.data.version }); }}>{label}</Checkbox>)}</fieldset>
      </Card>
      <Card>
        <div className="page-header-row"><div><h2>{text.shifts}</h2><p>{text.shiftYearNote}</p></div><Select aria-label={text.year} value={selectedYear ?? ""} options={(years.data?.items ?? []).map((year) => ({ value: String(year.id), label: year.label }))} onChange={(event) => setYearId(Number(event.target.value))} /></div>
        <div className="schedule-create-row"><Field id="shift-name" label={text.shiftName}><Input id="shift-name" value={name} onChange={(event) => setName(event.target.value)} /></Field><Field id="shift-order" label={text.displayOrder}><Input id="shift-order" type="number" min="1" max="99" value={order} onChange={(event) => setOrder(event.target.value)} /></Field><Button icon={<Plus size={18} />} loading={createShift.isPending} disabled={!selectedYear || !name.trim()} onClick={() => createShift.mutate({ name, displayOrder: Number(order), version: 0 }, { onSuccess: () => setName("") })}>{text.addShift}</Button></div>
        <div className="schedule-shift-list">{(shifts.data?.items ?? []).map((shift) => <Button variant={selectedShift?.id === shift.id ? "primary" : "secondary"} icon={<CalendarDays size={18} />} key={shift.id} aria-pressed={selectedShift?.id === shift.id} onClick={() => { setActiveShift(shift.id); setDraft(shift.periods); }}>{text.shiftLabel(shift.name, format.number(shift.lessonCount))}</Button>)}</div>
        {selectedShift && <div className="schedule-periods">
          <h3>{text.periodsFor(selectedShift.name)}</h3>
          <div className="schedule-generator"><Field id="period-start" label={text.firstStart}><Input id="period-start" type="time" value={firstStartTime} onChange={(event) => setFirstStartTime(event.target.value)} /></Field><Field id="lesson-minutes" label={text.lessonDuration}><Input id="lesson-minutes" type="number" min="10" max="120" value={lessonMinutes} onChange={(event) => setLessonMinutes(event.target.value)} /></Field><Field id="lesson-count" label={text.lessonCount}><Input id="lesson-count" type="number" min="1" max="12" value={lessonCount} onChange={(event) => setLessonCount(event.target.value)} /></Field><Field id="break-minutes" label={text.breakDuration}><Input id="break-minutes" type="number" min="5" max="120" value={breakMinutes} onChange={(event) => setBreakMinutes(event.target.value)} /></Field><Field id="break-after" label={text.breakAfter}><Input id="break-after" type="number" min="1" max="11" value={breakAfterLesson} onChange={(event) => setBreakAfterLesson(event.target.value)} /></Field><Button icon={<WandSparkles size={18} />} onClick={() => void generate()}>{text.generate}</Button></div>
          <div className="schedule-period-list">{draft.map((period, index) => <div className="schedule-period-row" key={`${period.position}-${index}`}><span>{period.kind === "lesson" ? text.lesson : text.break}</span><Input aria-label={`${text.startTime} ${format.number(index + 1)}`} type="time" value={period.startTime} onChange={(event) => setDraft((items) => items.map((item, row) => row === index ? { ...item, startTime: event.target.value } : item))} /><Input aria-label={`${text.endTime} ${format.number(index + 1)}`} type="time" value={period.endTime} onChange={(event) => setDraft((items) => items.map((item, row) => row === index ? { ...item, endTime: event.target.value } : item))} /><Checkbox checked={period.startBell} disabled={period.kind === "break"} onChange={(event) => setDraft((items) => items.map((item, row) => row === index ? { ...item, startBell: event.target.checked } : item))}>{text.startBell}</Checkbox><Checkbox checked={period.endBell} disabled={period.kind === "break"} onChange={(event) => setDraft((items) => items.map((item, row) => row === index ? { ...item, endBell: event.target.checked } : item))}>{text.endBell}</Checkbox></div>)}</div>
          <Button icon={<Save size={18} />} loading={savePeriods.isPending} disabled={draft.length === 0} onClick={() => savePeriods.mutate({ periods: draft.map((period) => ({ kind: period.kind, startTime: period.startTime, endTime: period.endTime, startBell: period.startBell, endBell: period.endBell })), version: selectedShift.version })}>{text.savePeriods}</Button>
        </div>}
      </Card>
      <Card>
        <div className="page-header-row"><h2>{text.bellSettings}</h2><BellRing aria-hidden="true" size={22} /></div>
        {bells.data && <div className="schedule-bell-row"><Field id="bell-tone" label={text.tone}><Select id="bell-tone" value={bells.data.tone} options={tones} onChange={(event) => updateBell.mutate({ tone: event.target.value as typeof bells.data.tone, breakBell: bells.data.breakBell, version: bells.data.version })} /></Field><Checkbox checked={bells.data.breakBell} onChange={(event) => updateBell.mutate({ tone: bells.data.tone, breakBell: event.target.checked, version: bells.data.version })}>{text.breakBell}</Checkbox><Button icon={<Volume2 size={18} />} variant="secondary" onClick={() => { setSoundError(false); void previewTone(bells.data!.tone).catch(() => setSoundError(true)); }}>{text.testSound}</Button></div>}
        {soundError && <Alert tone="error" message={text.soundUnavailable} />}
      </Card>
      <p className="schedule-note"><CalendarDays aria-hidden="true" size={16} />{text.previewOnly}</p>
    </section>
  );
}

const emptyShift: Shift = { id: 0, academicYearId: 0, name: "", displayOrder: 0, lessonCount: 0, periods: [], version: 0 };
