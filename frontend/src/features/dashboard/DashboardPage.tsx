import { RefreshCw, Wand2 } from "lucide-react";
import { Link } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Spinner } from "../../components/ui/spinner";
import { userErrorMessage } from "../../api";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { CurriculumStatusCard } from "./CurriculumStatusCard";
import { ReadinessCard } from "../readiness/ReadinessCard";
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
          {!summary.data.setupFinished && (
            <Card className="dashboard-card setup-resume" aria-labelledby="setup-resume-title">
              <h2 id="setup-resume-title">{messages.school.wizard.title}</h2>
              <p className="card-note">{messages.school.wizard.openHint}</p>
              <Link className="ui-button ui-button-primary ui-button-md setup-resume-link" to="/setup">
                <Wand2 aria-hidden="true" size={20} />
                <span>{messages.school.wizard.open}</span>
              </Link>
            </Card>
          )}
          <SetupChecklist steps={summary.data.checklist} />
          <ReadinessCard />
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
          <CurriculumStatusCard stages={summary.data.curriculum} format={format.number} />
        </div>
      )}
    </div>
  );
}
