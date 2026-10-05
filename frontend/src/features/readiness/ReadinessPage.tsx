import { CircleAlert, CircleCheck, ExternalLink, RefreshCw } from "lucide-react";
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
import { entityHref, findingMessage, fixHref, groupFindings } from "./readinessPresentation";
import { useReadiness } from "./readinessApi";

const text = messages.school.readiness;

export function ReadinessPage() {
  const format = useFormatter();
  const school = useSchoolContext();
  const yearId = school.data?.currentYear?.id;
  const readiness = useReadiness(yearId);
  const groups = readiness.data ? groupFindings(readiness.data.findings) : [];
  const checkedDate = readiness.data?.checkedAt.slice(0, 10);
  const checkedTime = readiness.data?.checkedAt.slice(11, 16);
  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} actions={(
        <Button icon={<RefreshCw aria-hidden="true" size={18} />} loading={readiness.isFetching} disabled={!yearId} onClick={() => void readiness.refetch()}>
          {text.refresh}
        </Button>
      )} />
      {!yearId && school.isSuccess && <Alert tone="warning" message={text.noYear} />}
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
              <Badge tone={readiness.data.errors ? "danger" : "success"}>{format.count(readiness.data.errors, "error")}</Badge>
              <Badge>{format.count(readiness.data.warnings, "warning")}</Badge>
            </div>
            <p className="readiness-meta"><span>{text.checkedSummary(format.date(checkedDate!), format.time(checkedTime!))}</span>
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
                {group.shortage > 0 && <p className="readiness-total-shortage">{format.count(group.shortage, "lesson")}</p>}
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