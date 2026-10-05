import { BookOpen, Layers3 } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { EmptyState } from "../../components/ui/empty-state";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useYearChoice, YearPicker } from "../academic-years/YearPicker";
import { CurriculumGrid, maxLessons, minLessons } from "./CurriculumGrid";
import { AddRepeatForm, CopyCurriculumTool, SetAcrossTool } from "./CurriculumHelpers";
import { useCurriculum, useSetCell, useSuggestedPreview } from "./curriculumApi";
import { DailySuggestionPanel } from "./DailySuggestionPanel";
import { SuggestedCurriculumPanel } from "./SuggestedCurriculumPanel";
import { SubjectTemplatePanel } from "./SubjectTemplatePanel";

const text = messages.school.curriculum;

/** The curriculum table with its totals and helpers for one year; shared by the screen and wizard step 5. */
export function CurriculumEditor({ yearId, children }: { yearId: number; children?: ReactNode }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const table = useCurriculum(yearId);
  const save = useSetCell(yearId);
  const data = table.data;
  const suggestion = useSuggestedPreview(yearId, []);
  const reviewStageIds = new Set((suggestion.data?.stages ?? []).filter((stage) => stage.needsReview).map((stage) => stage.stageId));

  return (
    <>
      <Card className="page-card" aria-labelledby="curriculum-title">
        <h2 id="curriculum-title">{text.title}</h2>
        <p id="curriculum-cell-hint" className="card-note">{text.cellHint(format.number(minLessons), format.number(maxLessons))}</p>
        {children}
        {data && data.stages.length > 0 && <SuggestedCurriculumPanel yearId={yearId} />}
        {table.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
        {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); void table.refetch(); }} loading={table.isFetching} />}
        <Alert tone="success" message={feedback.success} />
        <Alert tone="error" message={feedback.error} />
        {table.isPending && <Spinner label={messages.app.loadingContent} />}
        {data && data.stages.length === 0 && (
          <EmptyState icon={<Layers3 aria-hidden="true" size={24} />} message={text.noStages}
            action={<Link className="link-button" to="/classes/stages"><Layers3 aria-hidden="true" size={20} /><span>{messages.school.nav.stagesSections}</span></Link>} />
        )}
        {data && data.stages.length > 0 && data.rows.length === 0 && (
          <EmptyState icon={<BookOpen aria-hidden="true" size={24} />} message={text.noSubjects}
            action={<Link className="link-button" to="/classes/subjects"><BookOpen aria-hidden="true" size={20} /><span>{messages.school.nav.subjects}</span></Link>} />
        )}
        {data && data.stages.length > 0 && data.rows.length > 0 && (
          <CurriculumGrid table={data} format={format.number} saving={save.isPending} reviewStageIds={reviewStageIds}
            onSave={(input, done) => {
              feedback.reset();
              save.mutate(input, {
                onSuccess: () => { feedback.showSuccess(text.saved); done(true); },
                onError: (reason) => { feedback.showError(reason); done(false); },
              });
            }} />
        )}
      </Card>
      {data && data.stages.length > 0 && data.rows.length > 0 && (
        <Card className="page-card" aria-labelledby="curriculum-helpers-title">
          <h2 id="curriculum-helpers-title">{text.helpers}</h2>
          <AddRepeatForm yearId={yearId} table={data} />
          <CopyCurriculumTool yearId={yearId} table={data} format={format.number} />
          <SetAcrossTool yearId={yearId} table={data} format={format.number} />
        </Card>
      )}
      {data && data.stages.length > 0 && <DailySuggestionPanel yearId={yearId} />}
    </>
  );
}

/** المنهج الدراسي (spec 2.5 §3.3): weekly lessons of each subject per stage, with live totals against capacity. */
export function CurriculumPage() {
  const format = useFormatter();
  const choice = useYearChoice();
  const yearId = choice.yearId;

  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} />
      {choice.years.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      <Card className="page-card">
        <YearPicker id="curriculum-year" choice={choice} />
      </Card>
      {yearId !== null && (
        <>
          <Card className="page-card"><SubjectTemplatePanel yearId={yearId} format={format.number} /></Card>
          <CurriculumEditor key={`curriculum-${yearId}`} yearId={yearId} />
        </>
      )}
    </div>
  );
}
