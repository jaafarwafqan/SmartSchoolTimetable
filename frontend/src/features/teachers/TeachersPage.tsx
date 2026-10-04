import { ListPlus, Trash2, UserPlus, UsersRound } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { ArchiveBadge, RecordActions } from "../../components/RecordActions";
import { SearchField } from "../../components/SearchField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { Pagination } from "../../components/ui/pagination";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { weekdayLabel } from "../timetable-structure/weekdays";
import { BulkAddDialog } from "./BulkAddDialog";
import { TeacherDialog } from "./TeacherDialog";
import { useTeacherAction, useTeachers, type Teacher } from "./teachersApi";

const text = messages.school.teachers;
const common = messages.school.common;
const pageSize = 25;

/** المعلمون: search and filters, add/edit dialog with constraints, bulk add with preview, archive, confirmed delete. */
export function TeachersPage() {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const [filters, setFilters] = useState({ search: "", page: 1, includeArchived: false, releasedOnly: false });
  const [dialog, setDialog] = useState<{ open: boolean; teacher: Teacher | null; key: number }>({ open: false, teacher: null, key: 0 });
  const [bulk, setBulk] = useState({ open: false, key: 0 });
  const [deleting, setDeleting] = useState<Teacher | null>(null);
  const teachers = useTeachers({ ...filters, pageSize });
  const action = useTeacherAction();
  const rows = teachers.data?.items ?? [];
  const reload = () => { feedback.reset(); void teachers.refetch(); };
  const openDialog = (teacher: Teacher | null) => setDialog((current) => ({ open: true, teacher, key: current.key + 1 }));
  const closeDialog = () => setDialog((current) => ({ ...current, open: false }));
  const limit = (value: number | null) => (value === null ? text.noLimit : format.number(value));

  function toggleArchive(teacher: Teacher) {
    feedback.reset();
    action.mutate({ teacher, action: teacher.isArchived ? "restore" : "archive" }, {
      onSuccess: () => feedback.showSuccess(teacher.isArchived ? text.restored : text.archived),
      onError: feedback.showError,
    });
  }

  const columns: readonly TableColumn<Teacher>[] = [
    { key: "fullName", header: text.fullName, cell: (teacher) => teacher.fullName },
    { key: "shortName", header: text.shortName, cell: (teacher) => teacher.shortName },
    { key: "offDays", header: text.offDaysColumn, cell: (teacher) => teacher.offDays.map(weekdayLabel).join("، ") },
    { key: "blocked", header: text.blockedColumn, cell: (teacher) => format.number(teacher.blockedPeriods.length), numeric: true },
    { key: "limits", header: text.limitsColumn, cell: (teacher) => text.limitsValue(limit(teacher.maxLessonsPerDay), limit(teacher.maxLessonsPerWeek)) },
    {
      key: "status",
      header: common.status,
      cell: (teacher) => (
        <span className="row-actions">
          <ArchiveBadge archived={teacher.isArchived} />
          {teacher.fullyReleased && <Badge tone="warning" icon={<UsersRound aria-hidden="true" size={16} />}>{text.releasedBadge}</Badge>}
        </span>
      ),
    },
    {
      key: "actions",
      header: common.actions,
      cell: (teacher) => (
        <RecordActions name={teacher.fullName} archived={teacher.isArchived} onEdit={() => openDialog(teacher)}
          onToggleArchive={() => toggleArchive(teacher)} onDelete={() => setDeleting(teacher)} />
      ),
    },
  ];

  return (
    <div className="page">
      <PageHeader
        title={text.title}
        description={text.description}
        actions={(
          <>
            <Button icon={<UserPlus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.add}</Button>
            <Button variant="secondary" icon={<ListPlus aria-hidden="true" size={20} />} onClick={() => setBulk((current) => ({ open: true, key: current.key + 1 }))}>{text.bulkAdd}</Button>
          </>
        )}
      />
      {teachers.isError && <Alert tone="error" message={common.loadFailed} />}
      {feedback.conflict && <ConflictAlert onReload={reload} loading={teachers.isFetching} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <Card className="page-card">
        <div className="list-toolbar">
          <SearchField id="teacher-search" label={text.search} value={filters.search} onChange={(search) => setFilters({ ...filters, search, page: 1 })} />
          <Checkbox checked={filters.releasedOnly} onChange={(event) => setFilters({ ...filters, releasedOnly: event.target.checked, page: 1 })}>{text.releasedOnly}</Checkbox>
          <Checkbox checked={filters.includeArchived} onChange={(event) => setFilters({ ...filters, includeArchived: event.target.checked, page: 1 })}>{common.includeArchived}</Checkbox>
        </div>
        <DataTable
          caption={text.title}
          columns={columns}
          rows={rows}
          rowKey={(teacher) => String(teacher.id)}
          loading={teachers.isPending}
          empty={<EmptyState icon={<UsersRound aria-hidden="true" size={24} />} message={text.empty}
            action={<Button icon={<UserPlus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.add}</Button>} />}
        />
        {teachers.data && <Pagination page={filters.page} pageSize={pageSize} total={teachers.data.total} format={format} onPage={(page) => setFilters({ ...filters, page })} />}
      </Card>
      <TeacherDialog
        key={`teacher-dialog-${dialog.key}`}
        open={dialog.open}
        teacher={dialog.teacher}
        onClose={closeDialog}
        onReload={() => { closeDialog(); reload(); }}
        onSaved={() => { closeDialog(); feedback.showSuccess(text.saved); }}
      />
      <BulkAddDialog
        key={`bulk-dialog-${bulk.key}`}
        open={bulk.open}
        onClose={() => setBulk((current) => ({ ...current, open: false }))}
        onSaved={(count) => { setBulk((current) => ({ ...current, open: false })); feedback.showSuccess(text.bulkSaved(format.number(count))); }}
      />
      <ConfirmDialog
        open={deleting !== null}
        danger
        title={text.deleteTitle}
        consequence={text.deleteConsequence}
        confirmLabel={common.delete}
        confirmIcon={<Trash2 aria-hidden="true" size={20} />}
        loading={action.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && action.mutate({ teacher: deleting, action: "delete" }, {
          onSuccess: () => feedback.showSuccess(text.deleted),
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
    </div>
  );
}
