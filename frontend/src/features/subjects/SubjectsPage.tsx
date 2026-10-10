import { BookOpen, Wrench, Library } from "lucide-react";
import { useAllResources } from "../resources/resourcesApi";
import { OrphanBlockedNotice } from "../timetable-structure/OrphanBlockedNotice";
import { ArchiveBlockedDialog, GuardedDeleteDialog, isReferenceError } from "../../components/References";
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
  const [archiveBlocked, setArchiveBlocked] = useState<Subject | null>(null);
  const subjects = useSubjects({ search, page, pageSize, includeArchived });
  const resources = useAllResources();
  const resourceName = (id: number | null) => (id === null ? undefined : resources.data?.items.find((resource) => resource.id === id)?.name);
  const action = useSubjectAction();
  const create = useSaveSubject();
  const rows = subjects.data?.items ?? [];
  const reload = () => { feedback.reset(); void subjects.refetch(); };

  function quickAdd(form: FormData, element: HTMLFormElement) {
    addFeedback.reset();
    create.mutate({
      id: null,
      input: { name: String(form.get("newSubjectName") ?? ""), colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false, requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0 },
    }, {
      onSuccess: (created) => { element.reset(); addFeedback.showSuccess(text.added(created.name)); },
      onError: addFeedback.showError,
    });
  }

  function toggleArchive(subject: Subject) {
    feedback.reset();
    action.mutate({ subject, action: subject.isArchived ? "restore" : "archive" }, {
      onSuccess: () => feedback.showSuccess(subject.isArchived ? text.restored : text.archived),
      // A refused archive lists what still depends on the record instead of a bare error.
      onError: (error) => isReferenceError(error) ? setArchiveBlocked(subject) : feedback.showError(error),
    });
  }

  return (
    <div className="page">
      <PageHeader icon={Library} title={text.title} description={text.description} />
      <OrphanBlockedNotice />
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
                  {resourceName(subject.requiredResourceId) && <Badge tone="primary" icon={<Wrench aria-hidden="true" size={16} />}>{messages.school.requiredResource.badge(resourceName(subject.requiredResourceId) ?? "")}</Badge>}
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
      <GuardedDeleteDialog
        kind="subject"
        target={deleting && { id: deleting.id, name: deleting.name }}
        title={text.deleteTitle}
        consequence={text.deleteConsequence}
        loading={action.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && action.mutate({ subject: deleting, action: "delete" }, {
          onSuccess: () => feedback.showSuccess(text.deleted),
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
      <ArchiveBlockedDialog kind="subject" target={archiveBlocked && { id: archiveBlocked.id, name: archiveBlocked.name }}
        onClose={() => setArchiveBlocked(null)} />
    </div>
  );
}
