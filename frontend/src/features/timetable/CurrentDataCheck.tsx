import type { UseQueryResult } from "@tanstack/react-query";
import { CircleAlert, History, Info, ShieldAlert, ShieldCheck, UserRoundCheck, Wrench } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { SectionTitle } from "../../components/ui/section-title";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { changeItems, findingItems, type ChangeArea, type FindingItem } from "./currentCheck";
import type { CurrentCheck, TimetableVersionSummary } from "./timetableApi";

const text = messages.school.currentCheck;
const areaOrder: readonly ChangeArea[] = ["assignments", "availability", "loads", "curriculum", "timing", "other"];

type Props = {
  version: TimetableVersionSummary;
  check: UseQueryResult<CurrentCheck>;
  onShow: (item: FindingItem) => void;
  onRepair: () => void;
  onReplace: () => void;
  repairing: boolean;
  replacing: boolean;
};

/**
 * MF11 «الفحص على البيانات الحالية»: the saved version against today's school data (the version itself never changes): the
 * verdict, each problem as a plain Arabic sentence that opens its place in the grid, the two ways to fix it, and what changed
 * since the version was made. «الفحص وقت التوليد» (the snapshot check) stays a separate line above, so the two are never confused.
 */
export function CurrentDataCheck({ version, check, onShow, onRepair, onReplace, repairing, replacing }: Props) {
  const format = useFormatter();
  const data = check.data;
  const findings = data ? findingItems(data, format) : [];
  const changes = data ? changeItems(data, format) : [];
  const conflicts = findings.length > 0;
  const editable = version.status !== "archived";

  return (
    <section className="current-check" aria-labelledby="current-check-title">
      <SectionTitle level={3} icon={conflicts ? ShieldAlert : ShieldCheck} id="current-check-title">{text.title}</SectionTitle>
      <p className="card-note">{text.hint}</p>
      {check.isPending && <Spinner label={text.loading} />}
      {check.isError && <Alert tone="error" message={text.failed} />}
      {data && (
        <>
          {conflicts
            ? <Badge tone="danger" icon={<CircleAlert aria-hidden="true" size={16} />}>{text.status.conflicts(format.count(findings.length, "conflict"))}</Badge>
            : data.stale
              ? <Badge tone="warning" icon={<Info aria-hidden="true" size={16} />}>{text.status.changed}</Badge>
              : <Badge tone="success" icon={<ShieldCheck aria-hidden="true" size={16} />}>{text.status.fits}</Badge>}
          {conflicts && (
            <ul className="current-findings" aria-label={text.title}>
              {findings.map((item) => (
                <li key={item.key}>
                  <CircleAlert aria-hidden="true" size={18} />
                  <span>{item.sentence}</span>
                  <Button size="sm" variant="ghost" icon={<History aria-hidden="true" size={16} />} onClick={() => onShow(item)}>{text.show}</Button>
                </li>
              ))}
            </ul>
          )}
          {editable && (data.canReplaceTeachers || data.canRepair) && (
            <div className="version-actions">
              {data.canReplaceTeachers && (
                <Button icon={<UserRoundCheck aria-hidden="true" size={18} />} loading={replacing} onClick={onReplace}>{text.replace}</Button>
              )}
              {data.canRepair && (
                <Button variant={data.canReplaceTeachers ? "secondary" : "primary"} icon={<Wrench aria-hidden="true" size={18} />} loading={repairing} onClick={onRepair}>{text.repair}</Button>
              )}
            </div>
          )}
          {editable && data.canReplaceTeachers && <p className="card-note">{text.replaceHint}</p>}
          {editable && data.canRepair && <p className="card-note">{text.repairHint}</p>}
          <details className="advanced-options tool-panel current-changes" open={data.stale}>
            <summary><History aria-hidden="true" size={18} /><span>{text.changesTitle}</span></summary>
            {changes.length === 0
              ? <p className="card-note">{text.changesNone}</p>
              : areaOrder.filter((area) => changes.some((item) => item.area === area)).map((area) => (
                <div key={area}>
                  <h4>{text.areas[area]}</h4>
                  <ul>
                    {changes.filter((item) => item.area === area).map((item) => <li key={item.key}>{item.sentence}</li>)}
                  </ul>
                </div>
              ))}
          </details>
        </>
      )}
    </section>
  );
}
