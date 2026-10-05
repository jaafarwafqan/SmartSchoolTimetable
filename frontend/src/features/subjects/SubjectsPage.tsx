import { BookOpen, Trash2 } from "lucide-react";
import { useRef, useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { InlineAddForm } from "../../components/InlineAddForm";
import { ArchiveBadge, RecordActions } from "../../components/RecordActions";
import { SearchField } from "../../components/SearchField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { ExpandableRow } from "../../components/ui/expandable-row";
import { Pagination } from "../../components/ui/pagination";
import { subjectColorClasses, type SubjectColorIndex } from "../../components/ui/timetable-cell";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { SubjectEditor } from "./SubjectEditor";
import { useSaveSubject, useSubjectAction, useSubjects, type Subject } from "./subjectsApi";

const text = messages.school.subjects;
const common = messages.school.common;
const pageSize = 25;

/** المواد: quick add by name (Enter), search and archive filter, details edited in place on each row. */
export function SubjectsPage() {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const addFeedback = useFormFeedback();
  const addForm = useRef<HTMLFormElement>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [deleting, setDeleting] = useState<Subject | null>(null);
  const subjects = useSubjects({ search, page, pageSize, includeArchived });
  const action = useSubjectAction();
  const create = useSaveSubject();
  const rows = subjects.data?.items ?? [];
  const reload = () => { feedback.reset(); void subjects.refetch(); };

  function quickAdd(form: FormData, element: HTMLFormElement) {
    addFeedback.reset();
    create.mutate({
      id: null,
      input: { name: String(form.get("newSubjectName") ?? ""), colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false, requiresDoublePeriod: false, blockedPeriods: [], notes: null, version: 0 },
    }, {
      onSuccess: (created) => { element.reset(); addFeedback.showSuccess(text.added(created.name)); },
      onError: addFeedback.showError,
    });
  }

  function toggleArchive(subject: Subject) {
    feedback.reset();
    action.mutate({ subject, action: subject.isArchived ? "restore" : "archive" }, {
      onSuccess: () => feedback.showSuccess(subject.isArchived ? text.restored : text.archived),
      onError: feedback.showError,
    });
  }

  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} />
      <Card className="page-card">
        <Alert tone="success" message={addFeedback.success} />
        <Alert tone="error" message={addFeedback.error} />
        <InlineAddForm label={text.add} buttonLabel={text.addButton} pending={create.isPending} formRef={addForm} onSubmit={quickAdd} onInput={addFeedback.clearFieldFromEvent}>
          <TextField id="newSubjectName" label={text.newName} hint={text.quickAddHint} maxLength={80} required field="Name" errors={addFeedback.fieldErrors} />
        </InlineAddForm>
      </Card>
      {subjects.isError && <Alert tone="error" message={common.loadFailed} />}
      {feedback.conflict && <ConflictAlert onReload={reload} loading={subjects.isFetching} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <Card className="page-card">
        <div className="list-toolbar">
          <SearchField id="subject-search" label={text.search} value={search} onChange={(value) => { setSearch(value); setPage(1); }} />
          <Checkbox checked={includeArchived} onChange={(event) => { setIncludeArchived(event.target.checked); setPage(1); }}>{common.includeArchived}</Checkbox>
        </div>
        {subjects.isSuccess && rows.length === 0 && <EmptyState icon={<BookOpen aria-hidden="true" size={24} />} message={text.empty} />}
        <ul className="expandable-list" aria-label={text.title}>
          {rows.map((subject) => (
            <ExpandableRow
              key={subject.id}
              id={`subject-${subject.id}`}
              expanded={expandedId === subject.id}
              onToggle={() => setExpandedId(expandedId === subject.id ? null : subject.id)}
              summary={(
                <span className="row-summary">
                  <span className={`ui-subject-dot ${subjectColorClasses[subject.colorIndex as SubjectColorIndex] ?? ""}`} aria-hidden="true" />
                  <strong>{subject.name}</strong>
                  <Badge>{text.priorityValue(format.number(subject.priority))}</Badge>
                  {subject.blockedPeriods.length > 0 && <Badge>{text.blockedSummary(format.number(subject.blockedPeriods.length))}</Badge>}
                  <ArchiveBadge archived={subject.isArchived} />
                </span>
              )}
              actions={<RecordActions name={subject.name} archived={subject.isArchived} onEdit={() => setExpandedId(subject.id)} onToggleArchive={() => toggleArchive(subject)} onDelete={() => setDeleting(subject)} />}
            >
              <SubjectEditor subject={subject} onCancel={() => setExpandedId(null)} onReload={() => { setExpandedId(null); reload(); }}
                onSaved={() => { setExpandedId(null); feedback.showSuccess(text.saved); }} />
            </ExpandableRow>
          ))}
        </ul>
        {subjects.data && <Pagination page={page} pageSize={pageSize} total={subjects.data.total} format={format} onPage={setPage} />}
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
        onConfirm={() => deleting && action.mutate({ subject: deleting, action: "delete" }, {
          onSuccess: () => feedback.showSuccess(text.deleted),
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
    </div>
  );
}
