import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useYearChoice, YearPicker } from "../academic-years/YearPicker";
import { SectionsPanel } from "./SectionsPanel";
import { StageCardsPanel } from "./StageCardsPanel";
import { StageTemplatePanel } from "./StageTemplatePanel";
import { DailySuggestionPanel } from "../curriculum/DailySuggestionPanel";
import { StagesPanel } from "./StagesPanel";

const text = messages.school.stagesSections;

/** المراحل والشعب: stages of the selected year and the sections of the selected stage. */
export function StagesSectionsPage() {
  const choice = useYearChoice();
  const yearId = choice.yearId;

  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} />
      {choice.years.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      <Card className="page-card">
        <YearPicker id="stages-year" choice={choice} />
      </Card>
      {yearId !== null && <StageTemplatePanel key={`template-${yearId}`} yearId={yearId} />}
      {yearId !== null && <StageCardsPanel key={`cards-${yearId}`} yearId={yearId} />}
      {yearId !== null && <DailySuggestionPanel key={`daily-${yearId}`} yearId={yearId} />}
      {yearId !== null && (
        <StagesPanel
          key={`year-${yearId}`}
          yearId={yearId}
          renderSelected={(stage, includeArchived) => (
            <SectionsPanel key={`stage-${stage.id}`} yearId={yearId} stage={stage} includeArchived={includeArchived} />
          )}
        />
      )}
    </div>
  );
}
