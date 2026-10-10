import { SectionTitle } from "../../components/ui/section-title";
import { CalendarClock, Pencil, Plus, Trash2, Layers } from "lucide-react";
import { GuardedDeleteDialog } from "../../components/References";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { EmptyState } from "../../components/ui/empty-state";
import { IconButton } from "../../components/ui/icon-button";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { DayLessonsEditor } from "./DayLessonsEditor";
import { PeriodsEditor } from "./PeriodsEditor";
import { addMinutes } from "./periodRows";
import { ShiftDialog } from "./ShiftDialog";
import { useDeleteShift, useShifts, type Shift } from "./scheduleApi";

const text = messages.school.scheduleStructure;

/** Generator default (spec 2.5 §3.2): the evening shift starts 30 minutes after the morning shift ends. */
function generatorStart(shift: Shift, shifts: readonly Shift[]): string {
  const morning = shifts.find((item) => item.kind === "morning");
  const lastEnd = morning?.periods.at(-1)?.endTime;
  return shift.kind === "evening" && lastEnd ? addMinutes(lastEnd, 30) : "08:00";
}

/** Shifts of the selected year (table with edit/delete) and the periods editor of the selected shift. */
export function ShiftsCard({ yearId }: { yearId: number }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const shifts = useShifts(yearId);
  const remove = useDeleteShift(yearId);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [dialog, setDialog] = useState<{ open: boolean; shift: Shift | null }>({ open: false, shift: null });
  const [deleting, setDeleting] = useState<Shift | null>(null);
  const rows = shifts.data?.items ?? [];
  const selected = rows.find((shift) => shift.id === selectedId) ?? rows[0] ?? null;
  const reload = () => { feedback.reset(); void shifts.refetch(); };
  const openDialog = (shift: Shift | null) => setDialog({ open: true, shift });

  const columns: readonly TableColumn<Shift>[] = [
    { key: "name", header: text.shiftName, cell: (shift) => shift.name },
    { key: "order", header: text.displayOrder, cell: (shift) => format.number(shift.displayOrder), numeric: true },
    { key: "lessons", header: text.lessons, cell: (shift) => format.number(shift.lessonCount), numeric: true },
    {
      key: "actions",
      header: text.actions,
      cell: (shift) => (
        <span className="row-actions" role="group" aria-label={text.rowActions(shift.name)}>
          <IconButton aria-label={`${messages.school.common.edit} ${isolate(shift.name)}`} title={text.editShift} icon={<Pencil aria-hidden="true" size={16} />}
            onClick={(event) => { event.stopPropagation(); openDialog(shift); }} />
          <IconButton aria-label={`${messages.school.common.delete} ${isolate(shift.name)}`} title={text.deleteShiftTitle} icon={<Trash2 aria-hidden="true" size={16} />}
            onClick={(event) => { event.stopPropagation(); setDeleting(shift); }} />
        </span>
      ),
    },
  ];

  return (
    <Card className="page-card" aria-labelledby="shifts-title">
      <div className="card-header-row">
        <SectionTitle level={2} icon={Layers} id="shifts-title">{text.shifts}</SectionTitle>
        <Button variant="secondary" icon={<Plus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.addShift}</Button>
      </div>
      {shifts.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <DataTable
        caption={text.shifts}
        columns={columns}
        rows={rows}
        rowKey={(shift) => String(shift.id)}
        selectedKey={selected ? String(selected.id) : null}
        onSelect={(shift) => setSelectedId(shift.id)}
        loading={shifts.isPending}
        empty={<EmptyState icon={<CalendarClock aria-hidden="true" size={24} />} message={text.noShifts}
          action={<Button icon={<Plus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.addShift}</Button>} />}
      />
      {selected && <PeriodsEditor key={`shift-${selected.id}`} yearId={yearId} shift={selected} onReload={reload} onSaved={() => feedback.showSuccess(text.periodsSaved)} generatorStart={generatorStart(selected, rows)} />}
      {selected && <DayLessonsEditor key={`days-${selected.id}`} yearId={yearId} shift={selected} onReload={reload} onSaved={() => feedback.showSuccess(text.dayLessonsSaved)} />}
      <ShiftDialog
        open={dialog.open}
        yearId={yearId}
        shift={dialog.shift ? rows.find((shift) => shift.id === dialog.shift?.id) ?? dialog.shift : null}
        nextOrder={Math.min(99, rows.reduce((max, shift) => Math.max(max, shift.displayOrder), 0) + 1)}
        onClose={() => setDialog({ open: false, shift: null })}
        onReload={reload}
        onSaved={(shift) => { setDialog({ open: false, shift: null }); setSelectedId(shift.id); feedback.showSuccess(text.shiftSaved); }}
      />
      <GuardedDeleteDialog
        kind="shift"
        target={deleting && { id: deleting.id, name: deleting.name }}
        title={text.deleteShiftTitle}
        consequence={text.deleteShiftConsequence}
        loading={remove.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && remove.mutate({ id: deleting.id, version: deleting.version }, {
          onSuccess: () => feedback.showSuccess(text.shiftDeleted),
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
    </Card>
  );
}
