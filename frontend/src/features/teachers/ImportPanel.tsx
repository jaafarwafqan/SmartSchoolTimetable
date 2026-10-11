import { CircleAlert, CircleCheck, FileDown, FileUp, MinusCircle, Save, X } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { FileUpload } from "../../components/ui/file-upload";
import { SectionTitle } from "../../components/ui/section-title";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { teachersImportTemplateUrl, useApplyImport, usePreviewImport, type ImportIssue, type ImportPreview, type ImportRow } from "./teachersApi";

const text = messages.school.teachers.import;

type ImportPanelProps = { onClose: () => void; onDone: (summary: string) => void };

/**
 * Excel import of teachers with a dry run (M2): the template, the file, a preview of every row (Arabic notes), then only the valid rows are
 * added. A teacher who already exists is shown and left alone. A panel inside the page, never a drawer.
 */
export function ImportPanel({ onClose, onDone }: ImportPanelProps) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const previewImport = usePreviewImport();
  const applyImport = useApplyImport();

  function issueText(issue: ImportIssue): string {
    const issues = text.issues;
    switch (issue.code) {
      case "nameMissing": return issues.nameMissing;
      case "nameTooLong": return issues.nameTooLong;
      case "duplicateInFile": return issues.duplicateInFile(format.number(Number(issue.value ?? 0)));
      case "shortNameTooLong": return issues.shortNameTooLong;
      case "shortNameTaken": return issues.shortNameTaken(issue.value);
      case "limitInvalid": return issues.limitInvalid(issue.value);
      case "limitOrder": return issues.limitOrder;
      case "notesTooLong": return issues.notesTooLong;
      case "unknownSubject": return issues.unknownSubject(issue.value);
      case "shortNameProposed": return issues.shortNameProposed(issue.value);
      default: return issues.constraint;
    }
  }

  function choose(chosen: File) {
    feedback.reset();
    setFile(chosen);
    setPreview(null);
    previewImport.mutate(chosen, { onSuccess: setPreview, onError: (reason) => { setFile(null); feedback.showError(reason); } });
  }

  function apply() {
    if (!file) return;
    feedback.reset();
    applyImport.mutate(file, {
      onSuccess: (result) => onDone(text.done(format.number(result.created), format.number(result.existing), format.number(result.rejected))),
      onError: (reason) => { setPreview(null); feedback.showError(reason); },
    });
  }

  const columns: readonly TableColumn<ImportRow>[] = [
    { key: "row", header: text.row, numeric: true, cell: (row) => format.number(row.row) },
    { key: "name", header: text.fullName, cell: (row) => row.fullName },
    { key: "short", header: text.shortName, cell: (row) => row.shortName ?? "" },
    {
      key: "status",
      header: text.status,
      cell: (row) => row.status === "ready"
        ? <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.statuses.ready}</Badge>
        : row.status === "exists"
          ? <Badge tone="neutral" icon={<MinusCircle aria-hidden="true" size={16} />}>{text.statuses.exists}</Badge>
          : <Badge tone="danger" icon={<CircleAlert aria-hidden="true" size={16} />}>{text.statuses.error}</Badge>,
    },
    {
      key: "notes",
      header: text.notes,
      cell: (row) => row.issues.length === 0
        ? <span className="card-note">{text.noNotes}</span>
        : <ul className="import-issues">{row.issues.map((issue) => <li key={`${issue.code}-${issue.value ?? ""}`} data-severity={issue.severity}>{issueText(issue)}</li>)}</ul>,
    },
  ];

  return (
    <Card className="page-card bulk-panel" aria-labelledby="import-title">
      <div className="card-header-row">
        <SectionTitle level={2} icon={FileUp} id="import-title">{text.title}</SectionTitle>
      </div>
      <p className="ui-field-hint">{text.hint}</p>
      <Alert tone="error" message={feedback.error} />
      <div className="form-actions">
        <a className="link-button" href={teachersImportTemplateUrl} download>
          <FileDown aria-hidden="true" size={18} /><span>{text.downloadTemplate}</span>
        </a>
        <FileUpload id="teachers-import-file" label={text.chooseFile} loading={previewImport.isPending}
          accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onSelect={choose} />
      </div>
      {file && <p className="card-note">{text.chosenFile(file.name)}</p>}
      {preview && <p role="status" className="card-note">{text.summary(format.number(preview.ready), format.number(preview.existing), format.number(preview.errors), format.number(preview.warnings))}</p>}
      {preview && preview.ready === 0 && <Alert tone="warning" message={text.nothingReady} />}
      {preview && <DataTable caption={text.preview} columns={columns} rows={preview.rows} rowKey={(row) => String(row.row)} scrollable />}
      <div className="form-actions">
        <Button icon={<Save aria-hidden="true" size={20} />} loading={applyImport.isPending} disabled={!preview || preview.ready === 0} onClick={apply}>
          {text.apply(format.number(preview?.ready ?? 0))}
        </Button>
        <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={onClose}>{messages.app.cancel}</Button>
      </div>
    </Card>
  );
}
