import { SectionTitle } from "../../components/ui/section-title";
import { useMutation, useQuery } from "@tanstack/react-query";
import { ArchiveRestore, DatabaseBackup, HardDriveDownload } from "lucide-react";
import { useEffect, useState } from "react";
import { apiRequest } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Field } from "../../components/ui/field";
import { Input } from "../../components/ui/input";
import { messages } from "../../i18n/messages";
import { bootstrapQueryKey } from "../../lib/bootstrapQuery";
import { queryClient } from "../../lib/queryClient";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { SettingsSection } from "./SettingsSection";

const text = messages.school.backup;

type BackupResult = { filePath: string; sizeBytes: number; createdAt: string };
type RestoreResult = { restoredFrom: string; automaticBackupPath: string };

/**
 * «النسخ الاحتياطي والاستعادة» (Phase 4 M6): a backup goes to a folder the owner types; a restore needs the
 * checkbox AND the confirmation dialog, keeps an automatic backup of the current data, then signs the owner out.
 */
export function BackupSection() {
  const defaults = useQuery({ queryKey: ["backup", "defaults"], queryFn: () => apiRequest<{ suggestedFolder: string }>("/api/v1/backup/defaults") });
  const [folder, setFolder] = useState("");
  const [file, setFile] = useState("");
  const [replace, setReplace] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const backupFeedback = useFormFeedback();
  const restoreFeedback = useFormFeedback();
  useEffect(() => {
    if (defaults.data && folder === "") setFolder(defaults.data.suggestedFolder);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [defaults.data]);
  const create = useMutation({ mutationFn: () => apiRequest<BackupResult>("/api/v1/backup/", "POST", { folder: folder.trim() }) });
  const restore = useMutation({
    mutationFn: () => apiRequest<RestoreResult>("/api/v1/backup/restore", "POST", { filePath: file.trim(), confirm: true, confirmReplace: replace }),
  });

  return (
    <SettingsSection id="backup-section" icon={DatabaseBackup} title={text.title} description={text.description}>
      <Alert tone="error" message={backupFeedback.error} />
      <Alert tone="success" message={backupFeedback.success} />
      <div className="form-stack">
        <Field id="backup-folder" label={text.folder} hint={text.folderHint}>
          <Input id="backup-folder" dir="ltr" value={folder} autoComplete="off" aria-describedby="backup-folder-hint" onChange={(event) => setFolder(event.target.value)} />
        </Field>
        <div className="form-actions">
          <Button icon={<HardDriveDownload aria-hidden="true" size={18} />} loading={create.isPending} disabled={!folder.trim()}
            onClick={() => { backupFeedback.reset(); create.mutate(undefined, { onSuccess: (result) => backupFeedback.showSuccess(text.created(result.filePath)), onError: backupFeedback.showError }); }}>
            {text.create}
          </Button>
        </div>
      </div>

      <SectionTitle level={3} icon={ArchiveRestore} className="settings-subtitle">{text.restoreTitle}</SectionTitle>
      <Alert tone="error" message={restoreFeedback.error} />
      <div className="form-stack">
        <Field id="restore-file" label={text.restoreFile} hint={text.restoreFileHint}>
          <Input id="restore-file" dir="ltr" value={file} autoComplete="off" aria-describedby="restore-file-hint" onChange={(event) => setFile(event.target.value)} />
        </Field>
        <Checkbox checked={replace} onChange={(event) => setReplace(event.target.checked)}>{text.confirmReplace}</Checkbox>
        <div className="form-actions">
          <Button variant="danger" icon={<ArchiveRestore aria-hidden="true" size={18} />} disabled={!file.trim() || !replace} onClick={() => setConfirming(true)}>
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
