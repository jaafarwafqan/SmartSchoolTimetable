import { Trash2, X } from "lucide-react";
import { messages } from "../i18n/messages";
import { ApiRequestError } from "../i18n/errors";
import type { createFormatter } from "../lib/format";
import { useFormatter } from "../lib/schoolContext";
import { useReferences, type DependentGroup, type ReferenceKind, type ReferenceReport } from "../lib/referencesApi";
import { Alert } from "./ui/alert";
import { Button } from "./ui/button";
import { ConfirmDialog } from "./ui/confirm-dialog";
import { Dialog } from "./ui/dialog";

const text = messages.school.references;
const common = messages.school.common;

/** Error codes the reference guard returns; the matching dialog lists the dependents instead of a bare error. */
export const referenceErrorCodes: readonly string[] = ["RECORD_IN_USE", "CURRICULUM_IN_USE"];

export function isReferenceError(error: unknown): boolean {
  return error instanceof ApiRequestError && referenceErrorCodes.includes(error.code);
}

type Formatter = ReturnType<typeof createFormatter>;

/** "شعبتان (الأول / أ، الأول / ب)" or "٧ بنود في المنهج (… و٢ غيرها)، منها ١ في الأرشيف". */
export function describeGroup(group: DependentGroup, action: "delete" | "archive", format: Formatter): string {
  const total = action === "archive" ? group.active : group.active + group.archived;
  const counted = group.kind === "curriculumEntry"
    ? `${format.count(total, "line")} ${text.inCurriculum}`
    : format.count(total, group.kind);
  // The server lists active dependents first, so an archive notice names only the active ones.
  const shown = group.samples.slice(0, total);
  const hidden = total - shown.length;
  const samples = shown.join("، ") + (hidden > 0 ? ` ${text.more(format.number(hidden))}` : "");
  const archived = action === "delete" && group.archived > 0 ? `، ${text.archivedPart(format.number(group.archived))}` : "";
  return `${counted} (${samples})${archived}`;
}

/** The dependents that block the action, as an Arabic list; nothing when the action is allowed. */
export function ReferencesNotice({ report, action }: { report: ReferenceReport; action: "delete" | "archive" }) {
  const format = useFormatter();
  const blocked = action === "delete" ? report.deleteBlockedBy : report.archiveBlockedBy;
  if (!blocked) return null;
  const groups = report.dependents.filter((group) => (action === "delete" ? group.active + group.archived : group.active) > 0);
  return (
    <Alert tone="warning" message={action === "delete" ? text.deleteBlocked : text.archiveBlocked}>
      <ul className="reference-list">
        {groups.map((group) => <li key={group.kind}>{describeGroup(group, action, format)}</li>)}
      </ul>
      <span>{action === "delete" ? text.deleteHint : text.archiveHint}</span>
    </Alert>
  );
}

type Target = { id: number; name: string };

type GuardedDeleteDialogProps = {
  kind: ReferenceKind;
  target: Target | null;
  title: string;
  consequence: string;
  loading: boolean;
  onConfirm: () => void;
  onCancel: () => void;
};

/** Delete confirmation that first asks the reference guard; the delete button stays disabled while anything depends on the record. */
export function GuardedDeleteDialog({ kind, target, title, consequence, loading, onConfirm, onCancel }: GuardedDeleteDialogProps) {
  const references = useReferences(kind, target?.id ?? null);
  const report = references.data;
  const blocked = !report || report.id !== target?.id || report.deleteBlockedBy !== null;
  return (
    <ConfirmDialog
      open={target !== null}
      danger
      title={title}
      consequence={consequence}
      confirmLabel={common.delete}
      confirmIcon={<Trash2 aria-hidden="true" size={20} />}
      loading={loading}
      confirmDisabled={blocked}
      onCancel={onCancel}
      onConfirm={onConfirm}
    >
      {references.isPending && target !== null && <Alert tone="info" message={text.checking} />}
      {references.isError && <Alert tone="error" message={text.loadFailed} />}
      {report && <ReferencesNotice report={report} action="delete" />}
    </ConfirmDialog>
  );
}

/** Shown after an archive was refused: which active records still depend on the target. */
export function ArchiveBlockedDialog({ kind, target, onClose }: { kind: ReferenceKind; target: Target | null; onClose: () => void }) {
  const references = useReferences(kind, target?.id ?? null);
  return (
    <Dialog
      open={target !== null}
      title={text.archiveBlockedTitle(target?.name ?? "")}
      onClose={onClose}
      footer={<Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={onClose}>{messages.app.close}</Button>}
    >
      {references.isPending && target !== null && <Alert tone="info" message={text.checking} />}
      {references.isError && <Alert tone="error" message={text.loadFailed} />}
      {references.data && <ReferencesNotice report={references.data} action="archive" />}
    </Dialog>
  );
}
