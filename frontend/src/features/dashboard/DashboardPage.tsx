import { RefreshCw } from "lucide-react";
import { Link } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Spinner } from "../../components/ui/spinner";
import { userErrorMessage } from "../../api";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { countItems } from "./dashboardItems";
import { SetupChecklist } from "./SetupChecklist";
import { useDashboard } from "./useDashboard";

/** Dashboard: real counts from the database and the computed setup checklist (no placeholder statistics). */
export function DashboardPage() {
  const summary = useDashboard();
  const format = useFormatter();
  return (
    <div className="page">
      <PageHeader title={messages.school.nav.dashboard} description={messages.school.dashboard.description} />
      {summary.isPending && <Spinner label={messages.school.common.loading} />}
      {summary.isError && (
        <Alert tone="error" message={userErrorMessage(summary.error)}>
          <span>
            <Button size="sm" variant="secondary" icon={<RefreshCw aria-hidden="true" size={18} />} onClick={() => void summary.refetch()}>
              {messages.app.retry}
            </Button>
          </span>
        </Alert>
      )}
      {summary.data && (
        <div className="dashboard-grid">
          <SetupChecklist steps={summary.data.checklist} />
          <Card className="dashboard-card" aria-labelledby="counts-title">
            <h2 id="counts-title">{messages.school.dashboard.countsTitle}</h2>
            <dl className="count-grid">
              {summary.data.counts.filter((count) => countItems[count.key]).map((count) => (
                <div key={count.key} className="count-item">
                  <dt><Link to={countItems[count.key].to}>{countItems[count.key].label}</Link></dt>
                  <dd className="count-value">{format.number(count.value)}</dd>
                </div>
              ))}
            </dl>
          </Card>
        </div>
      )}
    </div>
  );
}
