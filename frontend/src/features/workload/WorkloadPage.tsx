import { Grid3x3, UsersRound } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { ChoiceCards } from "../../components/ui/choice-cards";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useYearChoice, YearPicker } from "../academic-years/YearPicker";
import { BulkActions } from "./BulkActions";
import { TeacherLoads } from "./TeacherLoads";
import { WorkloadMatrix } from "./WorkloadMatrix";
import { AssignmentSuggester } from "./AssignmentSuggester";
import { overloaded, useTeacherLoads, useWorkloadMatrix, type TeacherLoad } from "./workloadApi";

const text = messages.school.workload;
type View = "bySection" | "byTeacher";

/** Quick shortage warnings: teachers whose assigned lessons exceed what they can teach. */
function ShortageWarnings({ loads }: { loads: readonly TeacherLoad[] }) {
  const format = useFormatter();
  const over = overloaded(loads);
  if (over.length === 0) return null;
  return (
    <Alert tone="warning" message={text.shortageTitle}>
      <ul className="reference-list">
        {over.map((load) => (
          <li key={load.teacherId}>
            {text.shortage(load.fullName, format.number(load.assignedLessons), format.number(load.limit), format.number(load.assignedLessons - load.limit))}
          </li>
        ))}
      </ul>
    </Alert>
  );
}

/** «الأنصبة» (Phase 3 §4): assign a teacher to every curriculum line of every section, with live loads. */
export function WorkloadPage() {
  const choice = useYearChoice();
  const [view, setView] = useState<View>("bySection");
  const loads = useTeacherLoads(choice.yearId);
  const summary = useWorkloadMatrix(choice.yearId, null);
  const teacherLoads = (loads.data ?? []).filter((load) => !load.released || load.assignedLessons > 0);
  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} />
      <Card className="page-card">
        <YearPicker id="workload-year" choice={choice} />
      </Card>
      {choice.yearId !== null && (
        <>
          {loads.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
          {loads.data && <ShortageWarnings loads={loads.data} />}
          <Card className="page-card">
            <ChoiceCards<View> name="workload-view" legend={text.views.legend} value={view} onChange={setView} choices={[
              { value: "bySection", label: text.views.bySection, description: text.views.bySectionHint, icon: <Grid3x3 aria-hidden="true" size={20} /> },
              { value: "byTeacher", label: text.views.byTeacher, description: text.views.byTeacherHint, icon: <UsersRound aria-hidden="true" size={20} /> },
            ]} />
            {loads.isPending && <Spinner label={messages.app.loadingContent} />}
            {loads.data && view === "bySection" && <WorkloadMatrix yearId={choice.yearId} loads={teacherLoads} />}
            {loads.data && view === "byTeacher" && <TeacherLoads loads={loads.data} />}
          </Card>
          {loads.data && summary.data && summary.data.stages.length > 0 && (
            <>
              <AssignmentSuggester yearId={choice.yearId} />
              <BulkActions yearId={choice.yearId} loads={teacherLoads} stages={summary.data.stages} />
            </>
          )}
        </>
      )}
    </div>
  );
}
