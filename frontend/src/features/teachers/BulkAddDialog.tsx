import { CircleAlert, CircleCheck, Eye, Save, X } from "lucide-react";
import { useId, useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { Field } from "../../components/ui/field";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { Textarea } from "../../components/ui/textarea";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useCreateBulk, usePreviewBulk, type BulkPreview } from "./teachersApi";

const text = messages.school.teachers;
type PreviewLine = BulkPreview["lines"][number];

type BulkAddDialogProps = { open: boolean; onClose: () => void; onSaved: (count: number) => void };

/** Paste names one per line, preview how each line will be saved, then create the ready ones (spec 2.10). */
export function BulkAddDialog({ open, onClose, onSaved }: BulkAddDialogProps) {
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
      onSuccess: (result) => { feedback.reset(); onSaved(result.created); },
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
    <Dialog
      open={open}
      title={text.bulkTitle}
      description={text.bulkHint}
      onClose={close}
      footer={(
        <>
          <Button icon={<Save aria-hidden="true" size={20} />} loading={createBulk.isPending} disabled={ready.length === 0} onClick={saveReady}>
            {text.bulkSave(format.number(ready.length))}
          </Button>
          <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} loading={previewBulk.isPending} onClick={runPreview}>{text.bulkPreview}</Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={close}>{messages.app.cancel}</Button>
        </>
      )}
    >
      <div className="form-stack dialog-form">
        <Alert tone="error" message={feedback.error} />
        <Field id={fieldId} label={text.bulkNames} error={feedback.fieldErrors.Names}>
          <Textarea id={fieldId} rows={8} value={names} aria-invalid={feedback.fieldErrors.Names ? true : undefined}
            onChange={(event) => { setNames(event.target.value); setPreview(null); }} />
        </Field>
        {preview && preview.readyCount === 0 && <Alert tone="warning" message={text.bulkNothingReady} />}
        {preview && (
          <DataTable caption={text.bulkPreview} columns={columns} rows={preview.lines} rowKey={(line) => String(line.line)} />
        )}
      </div>
    </Dialog>
  );
}
