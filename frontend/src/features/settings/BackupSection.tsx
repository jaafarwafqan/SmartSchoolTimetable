import { SectionTitle } from "../../components/ui/section-title";
import { useMutation, useQuery } from "@tanstack/react-query";
import { ArchiveRestore, CircleAlert, CircleCheck, DatabaseBackup, FolderOpen, HardDriveDownload, Pencil } from "lucide-react";
import { useEffect, useState } from "react";
import { apiRequest } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { Field } from "../../components/ui/field";
import { Input } from "../../components/ui/input";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { bootstrapQueryKey } from "../../lib/bootstrapQuery";
import { queryClient } from "../../lib/queryClient";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { localDateTime } from "../history/historyPresentation";
import { sizeParts, useBackupFiles, type BackupFile } from "./backupApi";
import { FolderPickerDialog } from "./FolderPickerDialog";
import { SettingsSection } from "./SettingsSection";

const text = messages.school.backup;

type BackupResult = { filePath: string; sizeBytes: number; createdAt: string };
type RestoreResult = { restoredFrom: string; automaticBackupPath: string };

/**
 * «النسخ الاحتياطي والاستعادة»: the backup folder is chosen with the in-app folder picker (a typed path is the fallback);
 * a restore is chosen from the list of backups, needs the checkbox AND the confirmation dialog, keeps an automatic backup
 * of the current data, then signs the owner out.
 */
export function BackupSection() {
  const format = useFormatter();
  const defaults = useQuery({ queryKey: ["backup", "defaults"], queryFn: () => apiRequest<{ suggestedFolder: string }>("/api/v1/backup/defaults") });
  const [folder, setFolder] = useState("");
  const [picking, setPicking] = useState({ open: false, key: 0 });
  const [chosenFile, setChosenFile] = useState<string | null>(null);
  const [replace, setReplace] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const backupFeedback = useFormFeedback();
  const restoreFeedback = useFormFeedback();
  useEffect(() => {
    if (defaults.data && folder === "") setFolder(defaults.data.suggestedFolder);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [defaults.data]);
  const files = useBackupFiles(folder);
  const create = useMutation({
    mutationFn: () => apiRequest<BackupResult>("/api/v1/backup/", "POST", { folder: folder.trim() }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["backup", "files"] }),
  });
  const restore = useMutation({
    mutationFn: () => apiRequest<RestoreResult>("/api/v1/backup/restore", "POST", { filePath: chosenFile, confirm: true, confirmReplace: replace }),
  });
  const rows = files.data?.files ?? [];
  const chosen = rows.find((file) => file.path === chosenFile);

  const size = (file: BackupFile) => {
    const { unit, value } = sizeParts(file.sizeBytes);
    return unit === "mb" ? text.list.sizeMb(format.number(value)) : text.list.sizeKb(format.number(value));
  };
  const columns: readonly TableColumn<BackupFile>[] = [
    {
      key: "choose",
      header: text.list.action,
      cell: (file) => (
        <Button size="sm" variant={file.path === chosenFile ? "primary" : "secondary"} aria-pressed={file.path === chosenFile} disabled={!file.restorable}
          icon={<CircleCheck aria-hidden="true" size={16} />} onClick={() => { setChosenFile(file.path); setReplace(false); restoreFeedback.reset(); }}>
          {file.path === chosenFile ? text.list.chosen : text.list.choose}
        </Button>
      ),
    },
    {
      key: "when",
      header: text.list.when,
      cell: (file) => {
        const { date, time } = localDateTime(file.modifiedAt, format.preferences.timeZone);
        return <span className="nowrap">{text.list.at(format.date(date), format.time(time))}</span>;
      },
    },
    { key: "kind", header: text.list.kind, cell: (file) => <span className="nowrap">{text.list.kinds[file.kind]}</span> },
    { key: "size", header: text.list.size, numeric: true, cell: (file) => <span className="nowrap">{size(file)}</span> },
    {
      key: "status",
      header: text.list.status,
      cell: (file) => (file.restorable
        ? <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.list.restorable}</Badge>
        : <Badge tone="danger" icon={<CircleAlert aria-hidden="true" size={16} />}>{text.list.notRestorable}</Badge>),
    },
  ];

  return (
    <SettingsSection id="backup-section" icon={DatabaseBackup} title={text.title} description={text.description}>
      <Alert tone="error" message={backupFeedback.error} />
      <Alert tone="success" message={backupFeedback.success} />
      <div className="form-stack">
        <Field id="backup-folder" label={text.folder} hint={text.folderHint}>
          <output id="backup-folder" className="folder-path is-single-line" dir="ltr" title={folder || undefined} aria-describedby="backup-folder-hint">{folder || text.noFolder}</output>
        </Field>
        <div className="form-actions">
          <Button variant="secondary" icon={<FolderOpen aria-hidden="true" size={18} />} onClick={() => setPicking((current) => ({ open: true, key: current.key + 1 }))}>{text.chooseFolder}</Button>
          <Button icon={<HardDriveDownload aria-hidden="true" size={18} />} loading={create.isPending} disabled={!folder.trim()}
            onClick={() => { backupFeedback.reset(); create.mutate(undefined, { onSuccess: (result) => backupFeedback.showSuccess(text.created(result.filePath)), onError: backupFeedback.showError }); }}>
            {text.create}
          </Button>
        </div>
        <details className="advanced-options tool-panel">
          <summary><Pencil aria-hidden="true" size={18} /><span>{text.typed}</span></summary>
          <Field id="backup-folder-typed" label={text.typedLabel} hint={text.typedHint}>
            <Input id="backup-folder-typed" dir="ltr" value={folder} autoComplete="off" aria-describedby="backup-folder-typed-hint" onChange={(event) => setFolder(event.target.value)} />
          </Field>
        </details>
      </div>
      <FolderPickerDialog key={`folder-picker-${picking.key}`} open={picking.open} start={folder}
        onClose={() => setPicking((current) => ({ ...current, open: false }))}
        onChoose={(path) => { setFolder(path); setChosenFile(null); setPicking((current) => ({ ...current, open: false })); }} />

      <SectionTitle level={3} icon={ArchiveRestore} className="settings-subtitle">{text.restoreTitle}</SectionTitle>
      <p className="card-note">{text.list.hint}</p>
      <Alert tone="error" message={restoreFeedback.error} />
      <DataTable caption={text.list.title} columns={columns} rows={rows} rowKey={(file) => file.path} selectedKey={chosenFile} loading={files.isPending && folder.trim() !== ""} scrollable
        empty={<EmptyState icon={<ArchiveRestore aria-hidden="true" size={24} />} message={text.list.empty} />} />
      <div className="form-stack">
        {chosen && <p className="card-note">{text.list.chosenNote(format.date(localDateTime(chosen.modifiedAt, format.preferences.timeZone).date))}</p>}
        <Checkbox checked={replace} disabled={!chosen} onChange={(event) => setReplace(event.target.checked)}>{text.confirmReplace}</Checkbox>
        <div className="form-actions">
          <Button variant="danger" icon={<ArchiveRestore aria-hidden="true" size={18} />} disabled={!chosen || !replace} onClick={() => setConfirming(true)}>
            {text.restore}
          </Button>
        </div>
      </div>
      <ConfirmDialog open={confirming} title={text.restoreConfirmTitle} consequence={text.restoreConfirm} danger
        confirmLabel={text.restore} confirmIcon={<ArchiveRestore aria-hidden="true" size={18} />} loading={restore.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() => {
          restoreFeedback.reset();
          restore.mutate(undefined, {
            onSuccess: (result) => {
              setConfirming(false);
              // The owner is now signed out: show the note for a few seconds, then let the bootstrap refresh open the login screen.
              restoreFeedback.showSuccess(text.restored(result.automaticBackupPath));
              window.setTimeout(() => void queryClient.invalidateQueries({ queryKey: bootstrapQueryKey }), 4000);
            },
            onError: (error) => { setConfirming(false); restoreFeedback.showError(error); },
          });
        }} />
      <Alert tone="success" message={restoreFeedback.success} />
    </SettingsSection>
  );
}
