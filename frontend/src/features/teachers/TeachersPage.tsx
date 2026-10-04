import { ListPlus, Trash2, UsersRound } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { InlineAddForm } from "../../components/InlineAddForm";
import { ArchiveBadge, RecordActions } from "../../components/RecordActions";
import { SearchField } from "../../components/SearchField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { ExpandableRow } from "../../components/ui/expandable-row";
import { Pagination } from "../../components/ui/pagination";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { weekdayLabel } from "../timetable-structure/weekdays";
import { BulkAddPanel } from "./BulkAddPanel";
import { TeacherEditor } from "./TeacherEditor";
import { useSaveTeacher, useTeacherAction, useTeachers, type Teacher } from "./teachersApi";

const text = messages.school.teachers;
const common = messages.school.common;
const pageSize = 25;

/** المعلمون: quick add by full name (Enter), constraints edited in place, bulk add panel, archive, confirmed delete. */
export function TeachersPage() {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const addFeedback = useFormFeedback();
  const [filters, setFilters] = useState({ search: "", page: 1, includeArchived: false, releasedOnly: false });
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [bulk, setBulk] = useState({ open: false, key: 0 });
  const [deleting, setDeleting] = useState<Teacher | null>(null);
  const teachers = useTeachers({ ...filters, pageSize });
  const action = useTeacherAction();
  const create = useSaveTeacher();
  const rows = teachers.data?.items ?? [];
  const reload = () => { feedback.reset(); void teachers.refetch(); };
  const limit = (value: number | null) => (value === null ? text.noLimit : format.number(value));

  function quickAdd(form: FormData, element: HTMLFormElement) {
    addFeedback.reset();
    create.mutate({
      id: null,
      input: {
        fullName: String(form.get("newTeacherName") ?? ""), shortName: "", offDays: [], blockedPeriods: [], fullyReleased: false,
        releaseReason: null, releaseFrom: null, releaseTo: null, maxLessonsPerDay: null, maxLessonsPerWeek: null, notes: null, version: 0,
      },
    }, {
      onSuccess: (created) => { element.reset(); addFeedback.showSuccess(text.added(created.fullName, created.shortName)); },
      onError: addFeedback.showError,
    });
  }

  function toggleArchive(teacher: Teacher) {
    feedback.reset();
    action.mutate({ teacher, action: teacher.isArchived ? "restore" : "archive" }, {
      onSuccess: () => feedback.showSuccess(teacher.isArchived ? text.restored : text.archived),
      onError: feedback.showError,
    });
  }

  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description}
        actions={<Button variant="secondary" icon={<ListPlus aria-hidden="true" size={20} />} aria-expanded={bulk.open}
          onClick={() => setBulk((current) => ({ open: !current.open, key: current.key + 1 }))}>{text.bulkAdd}</Button>} />
      {bulk.open && (
        <BulkAddPanel key={`bulk-${bulk.key}`} onClose={() => setBulk((current) => ({ ...current, open: false }))}
          onSaved={(count) => { setBulk((current) => ({ ...current, open: false })); feedback.showSuccess(text.bulkSaved(format.number(count))); }} />
      )}
      <Card className="page-card">
        <Alert tone="success" message={addFeedback.success} />
        <Alert tone="error" message={addFeedback.error} />
        <InlineAddForm label={text.add} buttonLabel={text.addButton} pending={create.isPending} onSubmit={quickAdd} onInput={addFeedback.clearFieldFromEvent}>
          <TextField id="newTeacherName" label={text.newName} hint={text.quickAddHint} maxLength={150} required field="FullName" errors={addFeedback.fieldErrors} />
        </InlineAddForm>
      </Card>
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
        {teachers.isSuccess && rows.length === 0 && <EmptyState icon={<UsersRound aria-hidden="true" size={24} />} message={text.empty} />}
        <ul className="expandable-list" aria-label={text.title}>
          {rows.map((teacher) => (
            <ExpandableRow
              key={teacher.id}
              id={`teacher-${teacher.id}`}
              expanded={expandedId === teacher.id}
              onToggle={() => setExpandedId(expandedId === teacher.id ? null : teacher.id)}
              summary={(
                <span className="row-summary">
                  <strong>{teacher.fullName}</strong>
                  <span className="row-summary-muted">{teacher.shortName}</span>
                  {teacher.offDays.length > 0 && <Badge>{teacher.offDays.map(weekdayLabel).join("، ")}</Badge>}
                  <Badge>{text.limitsValue(limit(teacher.maxLessonsPerDay), limit(teacher.maxLessonsPerWeek))}</Badge>
                  {teacher.fullyReleased && <Badge tone="warning" icon={<UsersRound aria-hidden="true" size={16} />}>{text.releasedBadge}</Badge>}
                  <ArchiveBadge archived={teacher.isArchived} />
                </span>
              )}
              actions={<RecordActions name={teacher.fullName} archived={teacher.isArchived} onEdit={() => setExpandedId(teacher.id)} onToggleArchive={() => toggleArchive(teacher)} onDelete={() => setDeleting(teacher)} />}
            >
              <TeacherEditor teacher={teacher} onCancel={() => setExpandedId(null)} onReload={() => { setExpandedId(null); reload(); }}
                onSaved={() => { setExpandedId(null); feedback.showSuccess(text.saved); }} />
            </ExpandableRow>
          ))}
        </ul>
        {teachers.data && <Pagination page={filters.page} pageSize={pageSize} total={teachers.data.total} format={format} onPage={(page) => setFilters({ ...filters, page })} />}
      </Card>
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
