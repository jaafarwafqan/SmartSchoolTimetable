import { CalendarCheck, CalendarClock, CalendarDays, Megaphone, CalendarPlus, CalendarRange, CalendarX, Eye, EyeOff, List, Pencil, Trash2, TriangleAlert } from "lucide-react";
import { useState } from "react";
import { SearchField } from "../../components/SearchField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { IconButton } from "../../components/ui/icon-button";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { CalendarDayDialog } from "./CalendarDayDialog";
import { kindIcons } from "./calendarKinds";
import { IraqHolidaysCard } from "./IraqHolidaysCard";
import { useCalendarDays, useDeleteCalendarDay, useSetCalendarDayEnabled, type CalendarDay } from "./calendarApi";
import { monthStart } from "./monthGrid";
import { MonthView } from "./MonthView";

const text = messages.school.calendar;
const common = messages.school.common;

/** التقويم الدراسي: list and month view; add/edit dialog; confirmed delete; out-of-year entries are flagged. */
export function CalendarPage() {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const [view, setView] = useState<"list" | "month">("list");
  const [month, setMonth] = useState(() => monthStart(format.today()));
  const [search, setSearch] = useState("");
  const [dialog, setDialog] = useState<{ open: boolean; day: CalendarDay | null; key: number }>({ open: false, day: null, key: 0 });
  const [deleting, setDeleting] = useState<CalendarDay | null>(null);
  const [warning, setWarning] = useState<string | null>(null);
  const days = useCalendarDays({ search });
  const remove = useDeleteCalendarDay();
  const setEnabled = useSetCalendarDayEnabled();
  const rows = days.data?.items ?? [];
  const openDialog = (day: CalendarDay | null) => setDialog((current) => ({ open: true, day, key: current.key + 1 }));
  const closeDialog = () => setDialog((current) => ({ ...current, open: false }));

  const columns: readonly TableColumn<CalendarDay>[] = [
    {
      key: "title",
      header: text.titleField,
      cell: (day) => (
        <span className="calendar-title">
          <span className={`calendar-title${day.isEnabled ? "" : " is-disabled"}`}>{day.title}</span>
          {day.isApproximate && <Badge tone="warning" icon={<CalendarClock aria-hidden="true" size={16} />}>{text.approximate}</Badge>}
          {day.byDecision && <Badge tone="warning" icon={<Megaphone aria-hidden="true" size={16} />}>{text.byDecision}</Badge>}
        </span>
      ),
    },
    { key: "dates", header: text.dates, cell: (day) => (day.startDate === day.endDate ? format.date(day.startDate) : format.dateRange(day.startDate, day.endDate)) },
    {
      key: "kind",
      header: text.kind,
      cell: (day) => {
        const Icon = kindIcons[day.kind];
        return <span className={`calendar-kind kind-${day.kind}`}><Icon aria-hidden="true" size={16} />{text.kinds[day.kind]}</span>;
      },
    },
    {
      key: "status",
      header: common.status,
      cell: (day) => (
        <span className="row-actions">
          {!day.isEnabled && <Badge icon={<EyeOff aria-hidden="true" size={16} />}>{text.disabled}</Badge>}
          {day.isEnabled && (day.affectsSchedule
            ? <Badge tone="primary" icon={<CalendarCheck aria-hidden="true" size={16} />}>{text.affects}</Badge>
            : <Badge icon={<CalendarX aria-hidden="true" size={16} />}>{text.noEffect}</Badge>)}
          {day.outsideCurrentYear && <Badge tone="warning" icon={<TriangleAlert aria-hidden="true" size={16} />}>{text.outsideYear}</Badge>}
        </span>
      ),
    },
    {
      key: "actions",
      header: common.actions,
      cell: (day) => (
        <span className="row-actions" role="group" aria-label={common.rowActions(day.title)}>
          <IconButton aria-label={`${day.isEnabled ? text.disable : text.enable} ${isolate(day.title)}`} title={day.isEnabled ? text.disable : text.enable}
            icon={day.isEnabled ? <EyeOff aria-hidden="true" size={16} /> : <Eye aria-hidden="true" size={16} />}
            onClick={() => setEnabled.mutate({ day, enabled: !day.isEnabled }, {
              onSuccess: () => { feedback.reset(); feedback.showSuccess(day.isEnabled ? text.disabledDone : text.enabledDone); },
              onError: feedback.showError,
            })} />
          <IconButton aria-label={`${common.edit} ${isolate(day.title)}`} title={common.edit} icon={<Pencil aria-hidden="true" size={16} />} onClick={() => openDialog(day)} />
          <IconButton aria-label={`${common.delete} ${isolate(day.title)}`} title={common.delete} icon={<Trash2 aria-hidden="true" size={16} />} onClick={() => setDeleting(day)} />
        </span>
      ),
    },
  ];

  return (
    <div className="page">
      <PageHeader icon={CalendarRange} title={text.title} description={text.description}
        actions={<Button icon={<CalendarPlus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.add}</Button>} />
      <IraqHolidaysCard onImported={() => setWarning(null)} />
      {days.isError && <Alert tone="error" message={common.loadFailed} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="warning" message={warning} />
      <Alert tone="error" message={feedback.error} />
      <Card className="page-card">
        <div className="list-toolbar" role="group" aria-label={text.viewLabel}>
          <span className="row-actions">
            <Button size="sm" variant={view === "list" ? "primary" : "secondary"} aria-pressed={view === "list"} icon={<List aria-hidden="true" size={18} />} onClick={() => setView("list")}>{text.listView}</Button>
            <Button size="sm" variant={view === "month" ? "primary" : "secondary"} aria-pressed={view === "month"} icon={<CalendarDays aria-hidden="true" size={18} />} onClick={() => setView("month")}>{text.monthView}</Button>
          </span>
          {view === "list" && <SearchField id="calendar-search" label={text.search} value={search} onChange={setSearch} />}
        </div>
        {view === "list"
          ? (
            <DataTable caption={text.title} columns={columns} rows={rows} rowKey={(day) => String(day.id)} loading={days.isPending}
              empty={<EmptyState icon={<CalendarDays aria-hidden="true" size={24} />} message={text.empty} />} />
          )
          : <MonthView month={month} onMonth={setMonth} onOpen={openDialog} />}
      </Card>
      <CalendarDayDialog
        key={`calendar-dialog-${dialog.key}`}
        open={dialog.open}
        day={dialog.day}
        defaultDate={view === "month" ? month : undefined}
        onClose={closeDialog}
        onReload={() => { closeDialog(); void days.refetch(); }}
        onSaved={(day) => {
          closeDialog();
          feedback.reset();
          setWarning(day.outsideCurrentYear ? text.savedOutside : null);
          if (!day.outsideCurrentYear) feedback.showSuccess(text.saved);
        }}
      />
      <ConfirmDialog
        open={deleting !== null}
        danger
        title={text.deleteTitle}
        consequence={text.deleteConsequence}
        confirmLabel={common.delete}
        confirmIcon={<Trash2 aria-hidden="true" size={20} />}
        loading={remove.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && remove.mutate(deleting, {
          onSuccess: () => { setWarning(null); feedback.showSuccess(text.deleted); },
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
    </div>
  );
}
