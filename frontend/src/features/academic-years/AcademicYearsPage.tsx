import { CalendarPlus, CircleCheck, Pencil, Star, Trash2 } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { SearchField } from "../../components/SearchField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { EmptyState } from "../../components/ui/empty-state";
import { IconButton } from "../../components/ui/icon-button";
import { LtrText } from "../../components/ui/ltr-text";
import { Pagination } from "../../components/ui/pagination";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { TermsPanel } from "./TermsPanel";
import { YearDialog } from "./YearDialog";
import { useAcademicYears, useDeleteYear, useMakeYearCurrent, type AcademicYear } from "./yearsApi";

const text = messages.school.years;
const pageSize = 10;

/** السنة الدراسية والفصول: years list with search and paging, year dialog, terms of the selected year. */
export function AcademicYearsPage() {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [dialog, setDialog] = useState<{ open: boolean; year: AcademicYear | null }>({ open: false, year: null });
  const [deleting, setDeleting] = useState<AcademicYear | null>(null);
  const years = useAcademicYears({ search, page, pageSize });
  const makeCurrent = useMakeYearCurrent();
  const remove = useDeleteYear();
  const rows = years.data?.items ?? [];
  const selected = rows.find((year) => year.id === selectedId) ?? rows.find((year) => year.isCurrent) ?? null;
  const reload = () => { feedback.reset(); void years.refetch(); };

  const columns: readonly TableColumn<AcademicYear>[] = [
    { key: "label", header: text.label, cell: (year) => <LtrText>{year.label}</LtrText> },
    { key: "period", header: text.period, cell: (year) => format.dateRange(year.startDate, year.endDate) },
    { key: "terms", header: text.terms, cell: (year) => format.number(year.terms.length), numeric: true },
    {
      key: "status",
      header: text.status,
      cell: (year) => year.isCurrent
        ? <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.current}</Badge>
        : <Badge>{text.notCurrent}</Badge>,
    },
    {
      key: "actions",
      header: text.actions,
      cell: (year) => (
        <span className="row-actions" role="group" aria-label={text.rowActions(year.label)}>
          <IconButton aria-label={`${messages.school.common.edit} ${isolate(year.label)}`} title={text.edit} icon={<Pencil aria-hidden="true" size={16} />}
            onClick={(event) => { event.stopPropagation(); setDialog({ open: true, year }); }} />
          {!year.isCurrent && (
            <IconButton aria-label={`${text.makeCurrent}: ${isolate(year.label)}`} title={text.makeCurrent} icon={<Star aria-hidden="true" size={16} />}
              onClick={(event) => {
                event.stopPropagation();
                feedback.reset();
                makeCurrent.mutate({ id: year.id, version: year.version }, { onSuccess: () => feedback.showSuccess(text.madeCurrent), onError: feedback.showError });
              }} />
          )}
          <IconButton aria-label={`${messages.school.common.delete} ${isolate(year.label)}`} title={text.delete} icon={<Trash2 aria-hidden="true" size={16} />}
            onClick={(event) => { event.stopPropagation(); setDeleting(year); }} />
        </span>
      ),
    },
  ];

  return (
    <div className="page">
      <PageHeader
        title={messages.school.nav.academicYears}
        description={text.description}
        actions={<Button icon={<CalendarPlus aria-hidden="true" size={20} />} onClick={() => setDialog({ open: true, year: null })}>{text.add}</Button>}
      />
      {feedback.conflict && <ConflictAlert onReload={reload} loading={years.isFetching} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <Card className="page-card">
        <SearchField id="year-search" label={text.searchLabel} value={search} onChange={(value) => { setSearch(value); setPage(1); }} />
        <DataTable
          caption={messages.school.nav.academicYears}
          columns={columns}
          rows={rows}
          rowKey={(year) => String(year.id)}
          selectedKey={selected ? String(selected.id) : null}
          onSelect={(year) => setSelectedId(year.id)}
          loading={years.isPending}
          empty={(
            <EmptyState
              icon={<CalendarPlus aria-hidden="true" size={24} />}
              message={text.empty}
              action={<Button icon={<CalendarPlus aria-hidden="true" size={20} />} onClick={() => setDialog({ open: true, year: null })}>{text.add}</Button>}
            />
          )}
        />
        {years.data && <Pagination page={page} pageSize={pageSize} total={years.data.total} format={format} onPage={setPage} />}
      </Card>
      {selected
        ? <TermsPanel year={selected} onMessage={feedback.showSuccess} onError={feedback.showError} onReload={reload} />
        : rows.length > 0 && <Alert tone="info" message={text.selectYear} />}
      <YearDialog
        open={dialog.open}
        year={dialog.year ? rows.find((year) => year.id === dialog.year?.id) ?? dialog.year : null}
        onClose={() => setDialog({ open: false, year: null })}
        onReload={reload}
        onSaved={(year, message) => { setDialog({ open: false, year: null }); setSelectedId(year.id); feedback.showSuccess(message); }}
      />
      <ConfirmDialog
        open={deleting !== null}
        danger
        title={text.deleteTitle}
        consequence={text.deleteConsequence}
        confirmLabel={messages.school.common.delete}
        confirmIcon={<Trash2 aria-hidden="true" size={20} />}
        loading={remove.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => deleting && remove.mutate({ id: deleting.id, version: deleting.version }, {
          onSuccess: () => feedback.showSuccess(text.deleted),
          onError: feedback.showError,
          onSettled: () => setDeleting(null),
        })}
      />
    </div>
  );
}
