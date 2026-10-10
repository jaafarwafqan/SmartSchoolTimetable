import { ScrollText, History, Filter } from "lucide-react";
import { useState } from "react";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { EmptyState } from "../../components/ui/empty-state";
import { Field } from "../../components/ui/field";
import { Pagination } from "../../components/ui/pagination";
import { SectionTitle } from "../../components/ui/section-title";
import { Select } from "../../components/ui/select";
import { DataTable } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { auditCategories, useAuditHistory, type AuditCategory } from "./historyApi";
import { auditSentence, entryTime } from "./historyPresentation";

const text = messages.school.audit;
const pageSize = 25;

/** «سجل التغييرات»: the local audit history, newest first, filtered by type. Read-only. */
export function HistoryPage() {
  const format = useFormatter();
  const [category, setCategory] = useState<AuditCategory | "">("");
  const [page, setPage] = useState(1);
  const history = useAuditHistory(category, page, pageSize);
  return (
    <div className="page">
      <PageHeader icon={ScrollText} title={text.title} description={text.description} />
      <Card className="page-card" aria-labelledby="history-title">
        <SectionTitle level={2} icon={History} id="history-title">{text.title}</SectionTitle>
        <div className="timetable-controls">
          <Field id="history-category" label={text.categoryLabel}>
            <Select id="history-category" value={category} onChange={(event) => { setCategory(event.target.value as AuditCategory | ""); setPage(1); }}
              options={[{ value: "", label: text.all }, ...auditCategories.map((value) => ({ value, label: text.categories[value] }))]} />
          </Field>
        </div>
        {history.isError && <Alert tone="error" message={text.loadFailed}>{userErrorMessage(history.error)}</Alert>}
        <DataTable caption={text.title} rows={history.data?.items ?? []} rowKey={(entry) => String(entry.id)} loading={history.isPending} scrollable
          empty={<EmptyState icon={<Filter aria-hidden="true" size={32} />} message={category ? text.emptyFiltered : text.empty} />}
          columns={[
            { key: "when", header: text.when, cell: (entry) => entryTime(entry, format) },
            { key: "category", header: text.categoryLabel, cell: (entry) => text.categories[entry.category] },
            { key: "what", header: text.what, cell: (entry) => auditSentence(entry, format) },
          ]} />
        {history.data && <Pagination page={page} pageSize={pageSize} total={history.data.total} format={format} onPage={setPage} />}
      </Card>
    </div>
  );
}
