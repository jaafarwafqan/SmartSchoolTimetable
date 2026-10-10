import { CalendarClock, CalendarPlus, Landmark, ListChecks, X } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Dialog } from "../../components/ui/dialog";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { SectionTitle } from "../../components/ui/section-title";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useAcademicYears } from "../academic-years/yearsApi";
import { useImportIraqHolidays, useIraqHolidayPreview } from "./calendarApi";

const text = messages.school.calendar;
const iraq = text.iraq;

/** MF8: suggests the Iraqi official holidays of a chosen academic year; nothing is saved until the owner confirms. */
export function IraqHolidaysCard({ onImported }: { onImported: () => void }) {
  const format = useFormatter();
  const years = useAcademicYears({ search: "", page: 1, pageSize: 100 });
  const feedback = useFormFeedback();
  const [chosen, setChosen] = useState<number | null>(null);
  const [open, setOpen] = useState(false);
  const rows = years.data?.items ?? [];
  const yearId = chosen ?? rows.find((year) => year.isCurrent)?.id ?? rows[0]?.id ?? null;
  const preview = useIraqHolidayPreview(open ? yearId : null);
  const run = useImportIraqHolidays();
  const holidays = preview.data?.holidays ?? [];
  const fresh = holidays.filter((holiday) => !holiday.alreadyAdded);

  return (
    <Card className="page-card" aria-labelledby="iraq-holidays-title">
      <SectionTitle level={2} icon={Landmark} id="iraq-holidays-title">{iraq.title}</SectionTitle>
      <p className="card-note">{iraq.description}</p>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {rows.length === 0
        ? <p className="card-note">{iraq.noYears}</p>
        : (
          <div className="print-options-fields">
            <Field id="iraq-holidays-year" label={iraq.year}>
              <Select id="iraq-holidays-year" value={String(yearId ?? "")} onChange={(event) => setChosen(Number(event.target.value))}
                options={rows.map((year) => ({ value: String(year.id), label: year.label }))} />
            </Field>
            <Button variant="secondary" icon={<ListChecks aria-hidden="true" size={20} />} onClick={() => { feedback.reset(); setOpen(true); }}>{iraq.preview}</Button>
          </div>
        )}
      <Dialog open={open} title={iraq.dialogTitle} onClose={() => setOpen(false)}
        footer={(
          <>
            {fresh.length > 0 && (
              <Button icon={<CalendarPlus aria-hidden="true" size={20} />} loading={run.isPending}
                onClick={() => yearId !== null && run.mutate(yearId, {
                  onSuccess: (result) => { setOpen(false); feedback.showSuccess(iraq.done(format.count(result.added, "holiday"))); onImported(); },
                  onError: (error) => { setOpen(false); feedback.showError(error); },
                })}>{iraq.confirm(format.count(fresh.length, "holiday"))}</Button>
            )}
            <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={() => setOpen(false)}>{messages.app.cancel}</Button>
          </>
        )}>
        <div className="form-stack">
          <Alert tone="info" message={iraq.hint} />
          <p className="card-note">{iraq.review}</p>
          {preview.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
          {preview.isSuccess && fresh.length === 0 && <Alert tone="success" message={iraq.nothingNew} />}
          <ul className="holiday-preview">
            {holidays.map((holiday) => (
              <li key={`${holiday.key}-${holiday.startDate}`}>
                <span className="calendar-title">
                  <strong>{holiday.title}</strong>
                  <span>{holiday.startDate === holiday.endDate ? format.date(holiday.startDate) : format.dateRange(holiday.startDate, holiday.endDate)}</span>
                </span>
                <span className="row-actions">
                  {holiday.approximate && <Badge tone="warning" icon={<CalendarClock aria-hidden="true" size={16} />}>{text.approximate}</Badge>}
                  {holiday.alreadyAdded && <Badge>{iraq.added}</Badge>}
                </span>
              </li>
            ))}
          </ul>
        </div>
      </Dialog>
    </Card>
  );
}
