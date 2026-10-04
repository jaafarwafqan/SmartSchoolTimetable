import { Archive, CircleCheck, Pencil, RotateCcw, Trash2 } from "lucide-react";
import { Badge } from "../../components/ui/badge";
import { IconButton } from "../../components/ui/icon-button";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";

const text = messages.school.stagesSections;

/** Active/archived status with icon and text (never colour alone). */
export function ArchiveBadge({ archived }: { archived: boolean }) {
  return archived
    ? <Badge icon={<Archive aria-hidden="true" size={16} />}>{text.archived}</Badge>
    : <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.active}</Badge>;
}

type RecordActionsProps = {
  name: string;
  archived: boolean;
  onEdit: () => void;
  onToggleArchive: () => void;
  onDelete: () => void;
};

/** Dense-table row actions: edit, archive or restore, delete (delete always asks for confirmation). */
export function RecordActions({ name, archived, onEdit, onToggleArchive, onDelete }: RecordActionsProps) {
  const target = isolate(name);
  const archiveLabel = archived ? text.restore : text.archive;
  return (
    <span className="row-actions" role="group" aria-label={text.rowActions(name)}>
      {!archived && (
        <IconButton aria-label={`${messages.school.common.edit} ${target}`} title={messages.school.common.edit} icon={<Pencil aria-hidden="true" size={16} />}
          onClick={(event) => { event.stopPropagation(); onEdit(); }} />
      )}
      <IconButton aria-label={`${archiveLabel} ${target}`} title={archiveLabel}
        icon={archived ? <RotateCcw aria-hidden="true" size={16} /> : <Archive aria-hidden="true" size={16} />}
        onClick={(event) => { event.stopPropagation(); onToggleArchive(); }} />
      <IconButton aria-label={`${messages.school.common.delete} ${target}`} title={messages.school.common.delete} icon={<Trash2 aria-hidden="true" size={16} />}
        onClick={(event) => { event.stopPropagation(); onDelete(); }} />
    </span>
  );
}
