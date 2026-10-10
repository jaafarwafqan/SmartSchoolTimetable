import { CircleAlert, CircleCheck, ExternalLink, RefreshCw } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { LtrText } from "../../components/ui/ltr-text";
import { messages } from "../../i18n/messages";
import { isolate } from "../../i18n/isolate";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { entityHref, findingMessage, fixHref, groupFindings, errorCount, warningCount } from "./readinessPresentation";
import { Checkbox } from "../../components/ui/checkbox";
import { useReadiness } from "./readinessApi";

const text = messages.school.readiness;

/** A UTC timestamp as the local "YYYY-MM-DD" and "HH:mm" the formatter expects. */
function localDateTime(iso: string): { date: string; time: string } {
  const value = new Date(iso);
  const pad = (part: number) => String(part).padStart(2, "0");
  return { date: `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`, time: `${pad(value.getHours())}:${pad(value.getMinutes())}` };
}

export function ReadinessPage() {
  const format = useFormatter();
  const school = useSchoolContext();
  const yearId = school.data?.currentYear?.id;
  const [doublePeriods, setDoublePeriods] = useState(false);
  const readiness = useReadiness(yearId, doublePeriods);
  const groups = readiness.data ? groupFindings(readiness.data.findings) : [];
  // The check time is stored in UTC: show it in the computer's local time.
  const checked = readiness.data ? localDateTime(readiness.data.checkedAt) : null;
  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} actions={(
        <Button icon={<RefreshCw aria-hidden="true" size={18} />} loading={readiness.isFetching} disabled={!yearId} onClick={() => void readiness.refetch()}>
          {text.refresh}
        </Button>
      )} />
      {!yearId && school.isSuccess && <Alert tone="warning" message={text.noYear} />}
      {yearId && (
        <span className="form-stack">
          <Checkbox checked={doublePeriods} onChange={(event) => setDoublePeriods(event.target.checked)}>{text.doublePeriodsMode}</Checkbox>
          <span className="card-note">{text.doublePeriodsModeHint}</span>
        </span>
      )}
      {school.isError && <Alert tone="error" message={userErrorMessage(school.error)} />}
      {yearId && readiness.isPending && <p>{text.loading}</p>}
      {readiness.isError && <Alert tone="error" message={text.loadFailed}>{userErrorMessage(readiness.error)}</Alert>}
      {readiness.data && (
        <>
          <Card className="page-card readiness-summary">
            <p className={`readiness-status${readiness.data.ready ? " is-ready" : " has-errors"}`}>
              {readiness.data.ready ? <CircleCheck aria-hidden="true" size={24} /> : <CircleAlert aria-hidden="true" size={24} />}
              <strong>{readiness.data.ready ? text.ready : text.blocked}</strong>
            </p>
            <div className="readiness-counts">
              <Badge tone={readiness.data.errors ? "danger" : "success"}>{errorCount(readiness.data.errors, format)}</Badge>
              <Badge>{warningCount(readiness.data.warnings, format)}</Badge>
            </div>
            <p className="readiness-meta"><span>{text.checkedSummary(format.date(checked!.date), format.time(checked!.time))}</span>
              <span>{text.hashLabel} <LtrText>{text.shortHash(readiness.data.inputHash.slice(0, 12))}</LtrText></span></p>
          </Card>
          <section className="readiness-findings" aria-labelledby="readiness-findings-title">
            <h2 id="readiness-findings-title">{text.findings}</h2>
            {groups.length === 0 && <Alert tone="success" message={text.noFindings} />}
            {groups.map((group) => (
              <Card className="page-card readiness-group" key={group.key}>
                <div className="readiness-group-heading">
                  <h3>{group.entity.name ? isolate(group.entity.name) : text.title}</h3>
                  <Link className="link-button" to={entityHref(group.entity.kind)}>
                    <span>{messages.school.dashboard.openStep}</span><ExternalLink aria-hidden="true" size={16} />
                  </Link>
                </div>
                {group.shortage > 0 && <p className="readiness-total-shortage">{text.groupShortage(format.count(group.shortage, "lesson"))}</p>}
                <ul className="readiness-finding-list">
                  {group.findings.map((finding, index) => (
                    <li key={`${finding.code}-${index}`}>
                      <Badge tone={finding.severity === "error" ? "danger" : "warning"}>
                        {finding.severity === "error" ? text.errors : text.warnings}
                      </Badge>
                      <p>{findingMessage(finding, format)}</p>
                      {finding.related.length > 0 && <p className="readiness-related">{finding.related.map((entity) => isolate(entity.name)).join("، ")}</p>}
                      {finding.fixes[0] && <Link className="link-button" to={fixHref(finding.fixes[0])}>{text.fixes[finding.fixes[0] as keyof typeof text.fixes] ?? text.title}</Link>}
                    </li>
                  ))}
                </ul>
              </Card>
            ))}
          </section>
        </>
      )}
    </div>
  );
}