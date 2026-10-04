import { Layers3, Plus, Trash2 } from "lucide-react";
import { useState, type ReactNode } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SearchField } from "../../components/SearchField";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { ArchiveBadge, RecordActions } from "./RecordActions";
import { StageDialog } from "./StageDialog";
import { useStageAction, useStages, type Stage } from "./stagesApi";

const text = messages.school.stagesSections;

type StagesPanelProps = {
  yearId: number;
  /** Renders the panel of the selected stage (always the freshly loaded row). */
  renderSelected: (stage: Stage, includeArchived: boolean) => ReactNode;
};

/** Stages of the selected year: search, archive filter, add/edit dialog, archive/restore, confirmed delete. */
export function StagesPanel({ yearId, renderSelected }: StagesPanelProps) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const [search, setSearch] = useState("");
  const [includeArchived, setIncludeArchived] = useState(false);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [dialog, setDialog] = useState<{ open: boolean; stage: Stage | null }>({ open: false, stage: null });
  const [deleting, setDeleting] = useState<Stage | null>(null);
  const stages = useStages(yearId, search, includeArchived);
  const action = useStageAction(yearId);
  const rows = stages.data?.items ?? [];
  const selected = rows.find((stage) => stage.id === selectedId) ?? rows.find((stage) => !stage.isArchived) ?? null;
  const reload = () => { feedback.reset(); void stages.refetch(); };
  const openDialog = (stage: Stage | null) => setDialog({ open: true, stage });

  function toggleArchive(stage: Stage) {
    feedback.reset();
    action.mutate({ stage, action: stage.isArchived ? "restore" : "archive" }, {
      onSuccess: () => feedback.showSuccess(stage.isArchived ? text.stageRestored : text.stageArchived),
      onError: feedback.showError,
    });
  }

  const columns: readonly TableColumn<Stage>[] = [
    { key: "name", header: text.stageName, cell: (stage) => stage.name },
    { key: "order", header: text.order, cell: (stage) => format.number(stage.displayOrder), numeric: true },
    { key: "status", header: text.status, cell: (stage) => <ArchiveBadge archived={stage.isArchived} /> },
    {
      key: "actions",
      header: text.actions,
      cell: (stage) => (
        <RecordActions name={stage.name} archived={stage.isArchived} onEdit={() => openDialog(stage)}
          onToggleArchive={() => toggleArchive(stage)} onDelete={() => setDeleting(stage)} />
      ),
    },
  ];

  return (
    <>
      <Card className="page-card" aria-labelledby="stages-title">
        <div className="card-header-row">
          <h2 id="stages-title">{text.stages}</h2>
          <Button icon={<Plus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.addStage}</Button>
        </div>
        {stages.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
        {feedback.conflict && <ConflictAlert onReload={reload} loading={stages.isFetching} />}
        <Alert tone="success" message={feedback.success} />
        <Alert tone="error" message={feedback.error} />
        <div className="list-toolbar">
          <SearchField id="stage-search" label={text.searchStages} value={search} onChange={setSearch} />
          <Checkbox checked={includeArchived} onChange={(event) => setIncludeArchived(event.target.checked)}>{text.includeArchived}</Checkbox>
        </div>
        <DataTable
          caption={text.stages}
          columns={columns}
          rows={rows}
          rowKey={(stage) => String(stage.id)}
          selectedKey={selected ? String(selected.id) : null}
          onSelect={(stage) => setSelectedId(stage.id)}
          loading={stages.isPending}
          empty={<EmptyState icon={<Layers3 aria-hidden="true" size={24} />} message={text.noStages}
            action={<Button icon={<Plus aria-hidden="true" size={20} />} onClick={() => openDialog(null)}>{text.addStage}</Button>} />}
        />
        <StageDialog
          open={dialog.open}
          yearId={yearId}
          stage={dialog.stage ? rows.find((stage) => stage.id === dialog.stage?.id) ?? dialog.stage : null}
          nextOrder={Math.min(999, rows.reduce((max, stage) => Math.max(max, stage.displayOrder), 0) + 1)}
          onClose={() => setDialog({ open: false, stage: null })}
          onReload={reload}
          onSaved={(stage) => { setDialog({ open: false, stage: null }); setSelectedId(stage.id); feedback.showSuccess(text.stageSaved); }}
        />
        <ConfirmDialog
          open={deleting !== null}
          danger
          title={text.deleteStageTitle}
          consequence={text.deleteStageConsequence}
          confirmLabel={messages.school.common.delete}
          confirmIcon={<Trash2 aria-hidden="true" size={20} />}
          loading={action.isPending}
          onCancel={() => setDeleting(null)}
          onConfirm={() => deleting && action.mutate({ stage: deleting, action: "delete" }, {
            onSuccess: () => feedback.showSuccess(text.stageDeleted),
            onError: feedback.showError,
            onSettled: () => setDeleting(null),
          })}
        />
      </Card>
      {selected ? renderSelected(selected, includeArchived) : rows.length > 0 && <Alert tone="info" message={text.selectStage} />}
    </>
  );
}
