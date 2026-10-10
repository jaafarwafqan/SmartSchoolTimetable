import { SectionTitle } from "../../components/ui/section-title";
import { Plus, UsersRound, LayoutGrid } from "lucide-react";
import { ArchiveBlockedDialog, GuardedDeleteDialog, isReferenceError } from "../../components/References";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { EmptyState } from "../../components/ui/empty-state";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useShifts } from "../timetable-structure/scheduleApi";
import { ArchiveBadge, RecordActions } from "../../components/RecordActions";
import { SectionDialog } from "./SectionDialog";
import { useSectionAction, useSections, type Section, type Stage } from "./stagesApi";

const text = messages.school.stagesSections;

type SectionsPanelProps = { yearId: number; stage: Stage; includeArchived: boolean };

/** Sections of the selected stage with their shift and computed weekly capacity. */
export function SectionsPanel({ yearId, stage, includeArchived }: SectionsPanelProps) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const sections = useSections(yearId, stage.id, includeArchived);
  const shifts = useShifts(yearId);
  const action = useSectionAction(yearId, stage.id);
  const [dialog, setDialog] = useState<{ open: boolean; section: Section | null }>({ open: false, section: null });
  const [deleting, setDeleting] = useState<Section | null>(null);
  const [archiveBlocked, setArchiveBlocked] = useState<Section | null>(null);
  const rows = sections.data?.items ?? [];
  const shiftList = shifts.data?.items ?? [];
  const shiftOptions = shiftList.map((shift) => ({ value: String(shift.id), label: shift.name }));
  const shiftName = (id: number) => shiftList.find((shift) => shift.id === id)?.name ?? "";
  const canAdd = !stage.isArchived && shiftOptions.length > 0;
  const reload = () => { feedback.reset(); void sections.refetch(); };
  const openDialog = (section: Section | null) => setDialog({ open: true, section });

  function toggleArchive(section: Section) {
    feedback.reset();
    action.mutate({ section, action: section.isArchived ? "restore" : "archive" }, {
      onSuccess: () => feedback.showSuccess(section.isArchived ? text.sectionRestored : text.sectionArchived),
      // A refused archive lists what still depends on the record instead of a bare error.
      onError: (error) => isReferenceError(error) ? setArchiveBlocked(section) : feedback.showError(error),
    });
  }

  const columns: readonly TableColumn<Section>[] = [
    { key: "label", header: text.label, cell: (section) => section.label },
    { key: "shift", header: text.shift, cell: (section) => shiftName(section.shiftId) },
    { key: "students", header: text.studentsColumn, cell: (section) => (section.studentCount === null ? "" : format.number(section.studentCount)), numeric: true },
    { key: "capacity", header: text.capacity, cell: (section) => format.number(section.weeklyCapacity), numeric: true },
    { key: "status", header: text.status, cell: (section) => <ArchiveBadge archived={section.isArchived} /> },
    {
      key: "actions",
      header: text.actions,
      cell: (section) => (
        <RecordActions name={section.label} archived={section.isArchived} onEdit={() => openDialog(section)}
          onToggleArchive={() => toggleArchive(section)} onDelete={() => setDeleting(section)} />
      ),
    },
  ];

  return (
    <Card className="page-card" aria-labelledby="sections-title">
      <div className="card-header-row">
        <SectionTitle level={2} icon={LayoutGrid} id="sections-title">{text.sectionsOf(stage.name)}</SectionTitle>
        <Button variant="secondary" icon={<Plus aria-hidden="true" size={20} />} disabled={!canAdd} onClick={() => openDialog(null)}>{text.addSection}</Button>
      </div>
      {(sections.isError || shifts.isError) && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {shifts.isSuccess && shiftOptions.length === 0 && <Alert tone="warning" message={text.noShiftsYet} />}
      {feedback.conflict && <ConflictAlert onReload={reload} loading={sections.isFetching} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <p className="card-note">{text.capacityHint}</p>
      <DataTable
        caption={text.sectionsOf(stage.name)}
        columns={columns}
        rows={rows}
        rowKey={(section) => String(section.id)}
        loading={sections.isPending}
        empty={<EmptyState icon={<UsersRound aria-hidden="true" size={24} />} message={text.noSections}
          action={canAdd ? <Button icon={<Plus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.addSection}</Button> : undefined} />}
      />
      <SectionDialog
        open={dialog.open}
        yearId={yearId}
        stageId={stage.id}
        section={dialog.section ? rows.find((section) => section.id === dialog.section?.id) ?? dialog.section : null}
        shiftOptions={shiftOptions}
        onClose={() => setDialog({ open: false, section: null })}
        onReload={reload}
        onSaved={() => { setDialog({ open: false, section: null }); feedback.showSuccess(text.sectionSaved); }}
      />
      <GuardedDeleteDialog
        kind="section"
        target={deleting && { id: deleting.id, name: deleting.label }}
        title={text.deleteSectionTitle}
        consequence={text.deleteSectionConsequence}
        loading={action.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && action.mutate({ section: deleting, action: "delete" }, {
          onSuccess: () => feedback.showSuccess(text.sectionDeleted),
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
      <ArchiveBlockedDialog kind="section" target={archiveBlocked && { id: archiveBlocked.id, name: archiveBlocked.label }}
        onClose={() => setArchiveBlocked(null)} />
    </Card>
  );
}
