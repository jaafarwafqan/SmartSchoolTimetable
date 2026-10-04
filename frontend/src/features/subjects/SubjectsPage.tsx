import { BookOpen, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { ArchiveBadge, RecordActions } from "../../components/RecordActions";
import { SearchField } from "../../components/SearchField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { Pagination } from "../../components/ui/pagination";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { subjectColorClasses, type SubjectColorIndex } from "../../components/ui/timetable-cell";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { SubjectDialog } from "./SubjectDialog";
import { useSubjectAction, useSubjects, type Subject } from "./subjectsApi";

const text = messages.school.subjects;
const common = messages.school.common;
const pageSize = 25;

/** المواد: list with search, archive filter and paging; add/edit dialog; archive/restore; confirmed delete. */
export function SubjectsPage() {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [dialog, setDialog] = useState<{ open: boolean; subject: Subject | null; key: number }>({ open: false, subject: null, key: 0 });
  const [deleting, setDeleting] = useState<Subject | null>(null);
  const subjects = useSubjects({ search, page, pageSize, includeArchived });
  const action = useSubjectAction();
  const rows = subjects.data?.items ?? [];
  const reload = () => { feedback.reset(); void subjects.refetch(); };
  const openDialog = (subject: Subject | null) => setDialog((current) => ({ open: true, subject, key: current.key + 1 }));

  function toggleArchive(subject: Subject) {
    feedback.reset();
    action.mutate({ subject, action: subject.isArchived ? "restore" : "archive" }, {
      onSuccess: () => feedback.showSuccess(subject.isArchived ? text.restored : text.archived),
      onError: feedback.showError,
    });
  }

  const columns: readonly TableColumn<Subject>[] = [
    {
      key: "name",
      header: text.name,
      cell: (subject) => (
        <span className="ui-subject-chip">
          <span className={`ui-subject-dot ${subjectColorClasses[subject.colorIndex as SubjectColorIndex] ?? ""}`} aria-hidden="true" />
          {subject.name}
        </span>
      ),
    },
    { key: "priority", header: text.priority, cell: (subject) => format.number(subject.priority), numeric: true },
    { key: "blocked", header: text.blockedCount, cell: (subject) => format.number(subject.blockedPeriods.length), numeric: true },
    { key: "status", header: common.status, cell: (subject) => <ArchiveBadge archived={subject.isArchived} /> },
    {
      key: "actions",
      header: common.actions,
      cell: (subject) => (
        <RecordActions name={subject.name} archived={subject.isArchived} onEdit={() => openDialog(subject)}
          onToggleArchive={() => toggleArchive(subject)} onDelete={() => setDeleting(subject)} />
      ),
    },
  ];

  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description}
        actions={<Button icon={<Plus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.add}</Button>} />
      {subjects.isError && <Alert tone="error" message={common.loadFailed} />}
      {feedback.conflict && <ConflictAlert onReload={reload} loading={subjects.isFetching} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <Card className="page-card">
        <div className="list-toolbar">
          <SearchField id="subject-search" label={text.search} value={search} onChange={(value) => { setSearch(value); setPage(1); }} />
          <Checkbox checked={includeArchived} onChange={(event) => { setIncludeArchived(event.target.checked); setPage(1); }}>{common.includeArchived}</Checkbox>
        </div>
        <DataTable
          caption={text.title}
          columns={columns}
          rows={rows}
          rowKey={(subject) => String(subject.id)}
          loading={subjects.isPending}
          empty={<EmptyState icon={<BookOpen aria-hidden="true" size={24} />} message={text.empty}
            action={<Button icon={<Plus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.add}</Button>} />}
        />
        {subjects.data && <Pagination page={page} pageSize={pageSize} total={subjects.data.total} format={format} onPage={setPage} />}
      </Card>
      <SubjectDialog
        key={`subject-dialog-${dialog.key}`}
        open={dialog.open}
        subject={dialog.subject}
        onClose={() => setDialog((current) => ({ ...current, open: false }))}
        onReload={() => { setDialog((current) => ({ ...current, open: false })); reload(); }}
        onSaved={() => { setDialog((current) => ({ ...current, open: false })); feedback.showSuccess(text.saved); }}
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
        onConfirm={() => deleting && action.mutate({ subject: deleting, action: "delete" }, {
          onSuccess: () => feedback.showSuccess(text.deleted),
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
    </div>
  );
}
