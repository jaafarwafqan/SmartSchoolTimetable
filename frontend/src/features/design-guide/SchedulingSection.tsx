import { SectionTitle } from "../../components/ui/section-title";
import { CircleAlert, ExternalLink, Grid3x3, Gauge, ShieldCheck } from "lucide-react";
import { Link } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { LoadBar, LoadStatusBadge } from "../../components/LoadBar";
import { LtrText } from "../../components/ui/ltr-text";
import { GuideSection } from "./GuideSection";
import { guideMessages } from "./guideMessages";

const text = guideMessages.scheduling;
type ExampleRow = { id: string; section: string; teacher: string };
const rows: ExampleRow[] = [
  { id: "a", section: text.sectionA, teacher: text.teacher },
  { id: "b", section: text.sectionB, teacher: text.unassigned },
];
const columns: readonly TableColumn<ExampleRow>[] = [
  { key: "section", header: text.stage, cell: (row) => row.section },
  { key: "teacher", header: text.maths, cell: (row) => row.teacher },
];

/** DESIGN_SYSTEM.md Phase 3: the assignment matrix, teacher load meter, and grouped readiness finding. */
export function SchedulingSection() {
  return (
    <GuideSection id="guide-scheduling" title={text.title}>
      <SectionTitle level={3} icon={Grid3x3}>{text.matrix}</SectionTitle>
      <p>{text.matrixCaption}</p>
      <DataTable caption={text.matrixCaption} columns={columns} rows={rows} rowKey={(row) => row.id} />
      <p className="guide-unassigned"><CircleAlert aria-hidden="true" size={18} />{text.unassigned}</p>
      <p>{text.specialist}</p>
      <SectionTitle level={3} icon={Gauge}>{text.loadTitle}</SectionTitle>
      <div className="guide-load-example">
        <div className="guide-load-status"><LoadStatusBadge status="near" /><span>{text.loadSummary}</span></div>
        <LoadBar label={text.loadLabel} assigned={18} limit={24} status="near" />
      </div>
      <SectionTitle level={3} icon={ShieldCheck}>{text.readinessTitle}</SectionTitle>
      <p className="readiness-status has-errors"><CircleAlert aria-hidden="true" size={20} /><strong>{text.blocked}</strong></p>
      <Alert tone="error" message={text.finding} />
      <p className="guide-row"><Link className="link-button" to="/readiness"><span>{text.openReport}</span><ExternalLink aria-hidden="true" size={16} /></Link><LtrText>{text.hashSample}</LtrText></p>
    </GuideSection>
  );
}
