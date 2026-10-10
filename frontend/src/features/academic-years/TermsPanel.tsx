import { SectionTitle } from "../../components/ui/section-title";
import { CalendarPlus, CircleCheck, Pencil, Star, Trash2, CalendarDays } from "lucide-react";
import { useState } from "react";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { IconButton } from "../../components/ui/icon-button";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { TermDialog } from "./TermDialog";
import { useDeleteTerm, useMakeTermCurrent, type AcademicYear, type Term } from "./yearsApi";

const text = messages.school.years;

type TermsPanelProps = {
  year: AcademicYear;
  onMessage: (message: string) => void;
  onError: (reason: unknown) => void;
  onReload: () => void;
};

/** Terms of the selected year: list, add/edit dialog, make current, delete with confirmation. */
export function TermsPanel({ year, onMessage, onError, onReload }: TermsPanelProps) {
  const format = useFormatter();
  const [editing, setEditing] = useState<Term | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleting, setDeleting] = useState<Term | null>(null);
  const makeCurrent = useMakeTermCurrent();
  const remove = useDeleteTerm();

  const columns: readonly TableColumn<Term>[] = [
    { key: "name", header: text.termName, cell: (term) => term.name },
    { key: "period", header: text.period, cell: (term) => format.dateRange(term.startDate, term.endDate) },
    {
      key: "status",
      header: text.status,
      cell: (term) => term.isCurrent
        ? <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.currentTerm}</Badge>
        : null,
    },
    {
      key: "actions",
      header: text.actions,
      cell: (term) => (
        <span className="row-actions" role="group" aria-label={text.rowActions(term.name)}>
          <IconButton aria-label={`${messages.school.common.edit} ${isolate(term.name)}`} title={messages.school.common.edit} icon={<Pencil aria-hidden="true" size={16} />}
            onClick={() => { setEditing(term); setDialogOpen(true); }} />
          {!term.isCurrent && (
            <IconButton aria-label={`${text.makeCurrentTerm}: ${isolate(term.name)}`} title={text.makeCurrentTerm} icon={<Star aria-hidden="true" size={16} />}
              onClick={() => makeCurrent.mutate({ yearId: year.id, termId: term.id, version: year.version }, { onError })} />
          )}
          <IconButton aria-label={`${messages.school.common.delete} ${isolate(term.name)}`} title={text.deleteTerm} icon={<Trash2 aria-hidden="true" size={16} />}
            onClick={() => setDeleting(term)} />
        </span>
      ),
    },
  ];

  return (
    <Card className="page-card" aria-labelledby="terms-title">
      <div className="card-header-row">
        <SectionTitle level={2} icon={CalendarDays} id="terms-title">{text.termsOf(year.label)}</SectionTitle>
        <Button variant="secondary" icon={<CalendarPlus aria-hidden="true" size={20} />} onClick={() => { setEditing(null); setDialogOpen(true); }}>
          {text.addTerm}
        </Button>
      </div>
      <DataTable
        caption={text.termsOf(year.label)}
        columns={columns}
        rows={year.terms}
        rowKey={(term) => String(term.id)}
        empty={<EmptyState icon={<CalendarPlus aria-hidden="true" size={24} />} message={text.noTerms} />}
      />
      <TermDialog
        open={dialogOpen}
        year={year}
        term={editing}
        onClose={() => setDialogOpen(false)}
        onReload={onReload}
        onSaved={(message) => { setDialogOpen(false); onMessage(message); }}
      />
      <ConfirmDialog
        open={deleting !== null}
        danger
        title={text.deleteTermTitle}
        consequence={text.deleteTermConsequence}
        confirmLabel={messages.school.common.delete}
        confirmIcon={<Trash2 aria-hidden="true" size={20} />}
        loading={remove.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && remove.mutate({ yearId: year.id, termId: deleting.id, version: year.version }, {
          onError,
          onSettled: () => setDeleting(null),
        })}
      />
    </Card>
  );
}
