import { CircleAlert, CircleCheck, Eye, Save, X } from "lucide-react";
import { useId, useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Field } from "../../components/ui/field";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { Textarea } from "../../components/ui/textarea";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useCreateBulk, usePreviewBulk, type BulkPreview } from "./teachersApi";

const text = messages.school.teachers;
type PreviewLine = BulkPreview["lines"][number];

/** `closable` false (setup wizard): no cancel button; the panel clears itself after saving for the next batch. */
type BulkAddPanelProps = { onClose: () => void; onSaved: (count: number) => void; closable?: boolean };

/** Bulk pattern (DESIGN_SYSTEM.md 14): a panel inside the page; paste names, preview each line, save the ready ones. */
export function BulkAddPanel({ onClose, onSaved, closable = true }: BulkAddPanelProps) {
  const fieldId = useId();
  const format = useFormatter();
  const feedback = useFormFeedback();
  const [names, setNames] = useState("");
  const [preview, setPreview] = useState<BulkPreview | null>(null);
  const previewBulk = usePreviewBulk();
  const createBulk = useCreateBulk();
  const ready = preview?.lines.filter((line) => line.status === "ready") ?? [];

  function close() {
    feedback.reset();
    onClose();
  }

  function runPreview() {
    feedback.reset();
    previewBulk.mutate(names.split(/\r?\n/), { onSuccess: setPreview, onError: feedback.showError });
  }

  function saveReady() {
    feedback.reset();
    createBulk.mutate(ready.map((line) => line.fullName), {
      onSuccess: (result) => { feedback.reset(); setNames(""); setPreview(null); onSaved(result.created); },
      onError: (reason) => { setPreview(null); feedback.showError(reason); },
    });
  }

  const columns: readonly TableColumn<PreviewLine>[] = [
    { key: "line", header: text.bulkLine, cell: (line) => format.number(line.line), numeric: true },
    { key: "name", header: text.fullName, cell: (line) => line.fullName },
    { key: "short", header: text.bulkProposedShortName, cell: (line) => line.shortName ?? "" },
    {
      key: "status",
      header: text.bulkStatus,
      cell: (line) => line.status === "ready"
        ? <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.bulkStatuses.ready}</Badge>
        : <Badge tone="danger" icon={<CircleAlert aria-hidden="true" size={16} />}>{text.bulkStatuses[line.status]}</Badge>,
    },
  ];

  return (
    <Card className="page-card bulk-panel" aria-labelledby="bulk-title">
      <div className="card-header-row">
        <h2 id="bulk-title">{text.bulkTitle}</h2>
      </div>
      <p className="ui-field-hint">{text.bulkHint}</p>
      <Alert tone="error" message={feedback.error} />
      <Field id={fieldId} label={text.bulkNames} error={feedback.fieldErrors.Names}>
        <Textarea id={fieldId} rows={8} value={names} aria-invalid={feedback.fieldErrors.Names ? true : undefined}
          onChange={(event) => { setNames(event.target.value); setPreview(null); }} />
      </Field>
      {preview && preview.readyCount === 0 && <Alert tone="warning" message={text.bulkNothingReady} />}
      {preview && <DataTable caption={text.bulkPreview} columns={columns} rows={preview.lines} rowKey={(line) => String(line.line)} />}
      <div className="form-actions">
        <Button icon={<Save aria-hidden="true" size={20} />} loading={createBulk.isPending} disabled={ready.length === 0} onClick={saveReady}>
          {text.bulkSave(format.number(ready.length))}
        </Button>
        <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} loading={previewBulk.isPending} onClick={runPreview}>{text.bulkPreview}</Button>
        {closable && <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>}
      </div>
    </Card>
  );
}
