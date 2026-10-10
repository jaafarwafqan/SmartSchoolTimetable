import { Save, Warehouse, X, DoorOpen } from "lucide-react";
import { useRef, useState, type FormEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { InlineAddForm } from "../../components/InlineAddForm";
import { ArchiveBadge, RecordActions } from "../../components/RecordActions";
import { ArchiveBlockedDialog, GuardedDeleteDialog, isReferenceError } from "../../components/References";
import { SearchField } from "../../components/SearchField";
import { SelectField } from "../../components/SelectField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { EmptyState } from "../../components/ui/empty-state";
import { ExpandableRow } from "../../components/ui/expandable-row";
import { Field } from "../../components/ui/field";
import { Pagination } from "../../components/ui/pagination";
import { Stepper } from "../../components/ui/stepper";
import { Textarea } from "../../components/ui/textarea";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { maxCapacity, minCapacity, resourceKinds, useResourceAction, useResources, useSaveResource, type Resource, type ResourceKind } from "./resourcesApi";

const text = messages.school.resources;
const common = messages.school.common;
const pageSize = 25;
const kindOptions = resourceKinds.map((kind) => ({ value: kind, label: text.kinds[kind] }));

function CapacityField({ id, value, onChange }: { id: string; value: number; onChange: (value: number) => void }) {
  const format = useFormatter();
  return (
    <Field id={id} label={text.capacity} hint={text.capacityHint}>
      <Stepper id={id} label={text.capacity} value={value} min={minCapacity} max={maxCapacity} format={format.number}
        decreaseLabel={text.decreaseCapacity} increaseLabel={text.increaseCapacity} onChange={onChange} />
    </Field>
  );
}

function ResourceEditor({ resource, onSaved, onCancel, onReload }: { resource: Resource; onSaved: () => void; onCancel: () => void; onReload: () => void }) {
  const prefix = `resource-${resource.id}`;
  const feedback = useFormFeedback();
  const save = useSaveResource();
  const [capacity, setCapacity] = useState(resource.capacity);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    save.mutate({
      id: resource.id,
      input: {
        name: String(form.get(`${prefix}-name`) ?? ""),
        kind: String(form.get(`${prefix}-kind`) ?? "other") as ResourceKind,
        capacity,
        notes: String(form.get(`${prefix}-notes`) ?? "").trim() || null,
        version: resource.version,
      },
    }, { onSuccess: () => { feedback.reset(); onSaved(); }, onError: feedback.showError });
  }

  return (
    <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="error" message={feedback.error} />
      <div className="form-grid">
        <TextField id={`${prefix}-name`} label={text.name} defaultValue={resource.name} maxLength={80} required field="Name" errors={feedback.fieldErrors} />
        <SelectField id={`${prefix}-kind`} label={text.kind} options={kindOptions} defaultValue={resource.kind} required field="Kind" errors={feedback.fieldErrors} />
      </div>
      <CapacityField id={`${prefix}-capacity`} value={capacity} onChange={setCapacity} />
      <Field id={`${prefix}-notes`} label={text.notes} error={feedback.fieldErrors.Notes}>
        <Textarea id={`${prefix}-notes`} name={`${prefix}-notes`} defaultValue={resource.notes ?? ""} maxLength={500} data-field="Notes" />
      </Field>
      <div className="form-actions">
        <Button type="submit" icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}>{text.save}</Button>
        <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={onCancel}>{messages.app.cancel}</Button>
      </div>
    </form>
  );
}

/** الموارد (Phase 3 §4): quick add with name, kind and capacity; details edited in place on each row. */
export function ResourcesPage() {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const addFeedback = useFormFeedback();
  const addForm = useRef<HTMLFormElement>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [deleting, setDeleting] = useState<Resource | null>(null);
  const [archiveBlocked, setArchiveBlocked] = useState<Resource | null>(null);
  const [newCapacity, setNewCapacity] = useState(minCapacity);
  const resources = useResources({ search, page, pageSize, includeArchived });
  const action = useResourceAction();
  const create = useSaveResource();
  const rows = resources.data?.items ?? [];
  const reload = () => { feedback.reset(); void resources.refetch(); };

  function quickAdd(form: FormData, element: HTMLFormElement) {
    addFeedback.reset();
    create.mutate({
      id: null,
      input: { name: String(form.get("newResourceName") ?? ""), kind: String(form.get("newResourceKind") ?? "other") as ResourceKind, capacity: newCapacity, notes: null, version: 0 },
    }, {
      onSuccess: (created) => { element.reset(); setNewCapacity(minCapacity); addFeedback.showSuccess(text.added(created.name)); },
      onError: addFeedback.showError,
    });
  }

  function toggleArchive(resource: Resource) {
    feedback.reset();
    action.mutate({ resource, action: resource.isArchived ? "restore" : "archive" }, {
      onSuccess: () => feedback.showSuccess(resource.isArchived ? text.restored : text.archived),
      onError: (error) => isReferenceError(error) ? setArchiveBlocked(resource) : feedback.showError(error),
    });
  }

  return (
    <div className="page">
      <PageHeader icon={DoorOpen} title={text.title} description={text.description} />
      <Card className="page-card">
        <Alert tone="success" message={addFeedback.success} />
        <Alert tone="error" message={addFeedback.error} />
        <InlineAddForm label={text.add} buttonLabel={text.addButton} pending={create.isPending} formRef={addForm} onSubmit={quickAdd} onInput={addFeedback.clearFieldFromEvent}>
          <TextField id="newResourceName" label={text.name} hint={text.quickAddHint} maxLength={80} required field="Name" errors={addFeedback.fieldErrors} />
          <SelectField id="newResourceKind" label={text.kind} options={kindOptions} defaultValue="lab" required field="Kind" errors={addFeedback.fieldErrors} />
          <CapacityField id="newResourceCapacity" value={newCapacity} onChange={setNewCapacity} />
        </InlineAddForm>
      </Card>
      {resources.isError && <Alert tone="error" message={common.loadFailed} />}
      {feedback.conflict && <ConflictAlert onReload={reload} loading={resources.isFetching} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <Card className="page-card">
        <div className="list-toolbar">
          <SearchField id="resource-search" label={text.search} value={search} onChange={(value) => { setSearch(value); setPage(1); }} />
          <Checkbox checked={includeArchived} onChange={(event) => { setIncludeArchived(event.target.checked); setPage(1); }}>{common.includeArchived}</Checkbox>
        </div>
        {resources.isSuccess && rows.length === 0 && <EmptyState icon={<Warehouse aria-hidden="true" size={24} />} message={text.empty} />}
        <ul className="expandable-list" aria-label={text.title}>
          {rows.map((resource) => (
            <ExpandableRow
              key={resource.id}
              id={`resource-${resource.id}`}
              expanded={expandedId === resource.id}
              onToggle={() => setExpandedId(expandedId === resource.id ? null : resource.id)}
              summary={(
                <span className="row-summary">
                  <strong>{resource.name}</strong>
                  <Badge>{text.kinds[resource.kind]}</Badge>
                  <Badge>{text.capacityValue(format.number(resource.capacity))}</Badge>
                  <ArchiveBadge archived={resource.isArchived} />
                </span>
              )}
              actions={<RecordActions name={resource.name} archived={resource.isArchived} onEdit={() => setExpandedId(resource.id)} onToggleArchive={() => toggleArchive(resource)} onDelete={() => setDeleting(resource)} />}
            >
              <ResourceEditor resource={resource} onCancel={() => setExpandedId(null)} onReload={() => { setExpandedId(null); reload(); }}
                onSaved={() => { setExpandedId(null); feedback.showSuccess(text.saved); }} />
            </ExpandableRow>
          ))}
        </ul>
        {resources.data && <Pagination page={page} pageSize={pageSize} total={resources.data.total} format={format} onPage={setPage} />}
      </Card>
      <GuardedDeleteDialog
        kind="resource"
        target={deleting && { id: deleting.id, name: deleting.name }}
        title={text.deleteTitle}
        consequence={text.deleteConsequence}
        loading={action.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && action.mutate({ resource: deleting, action: "delete" }, {
          onSuccess: () => feedback.showSuccess(text.deleted),
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
      <ArchiveBlockedDialog kind="resource" target={archiveBlocked && { id: archiveBlocked.id, name: archiveBlocked.name }}
        onClose={() => setArchiveBlocked(null)} />
    </div>
  );
}
