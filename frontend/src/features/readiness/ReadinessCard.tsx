import { CircleAlert, CircleCheck, ChevronLeft } from "lucide-react";
import { Link } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useSchoolContext } from "../../lib/schoolContext";
import { errorCount, warningCount } from "./readinessPresentation";
import { useReadiness } from "./readinessApi";

const text = messages.school.readiness;

export function ReadinessCard() {
  const format = useFormatter();
  const school = useSchoolContext();
  const yearId = school.data?.currentYear?.id;
  const readiness = useReadiness(yearId);
  return (
    <Card className="dashboard-card readiness-card" aria-labelledby="readiness-card-title">
      <h2 id="readiness-card-title">{text.title}</h2>
      {!yearId && <Alert tone="warning" message={text.noYear} />}
      {readiness.isPending && yearId !== undefined && <p>{text.loading}</p>}
      {readiness.isError && <Alert tone="error" message={text.loadFailed} />}
      {readiness.data && (
        <>
          <p className={`readiness-status${readiness.data.ready ? " is-ready" : " has-errors"}`}>
            {readiness.data.ready ? <CircleCheck aria-hidden="true" size={20} /> : <CircleAlert aria-hidden="true" size={20} />}
            <strong>{readiness.data.ready ? text.ready : text.blocked}</strong>
          </p>
          <p>{text.countSummary(errorCount(readiness.data.errors, format), warningCount(readiness.data.warnings, format))}</p>
        </>
      )}
      <Link className="link-button" to={yearId ? "/readiness" : "/school/year"}>
        <span>{messages.school.dashboard.openStep}</span>
        <ChevronLeft aria-hidden="true" size={18} />
      </Link>
    </Card>
  );
}