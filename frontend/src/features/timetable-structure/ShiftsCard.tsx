import { SectionTitle } from "../../components/ui/section-title";
import { ListOrdered } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { DayLessonsEditor } from "./DayLessonsEditor";
import { PeriodsEditor } from "./PeriodsEditor";
import { useShifts } from "./scheduleApi";

const text = messages.school.scheduleStructure;

/**
 * The year's single shift (MF7: sections never differ by shift), its period rows and per-day lessons, for fine edits such
 * as one bell or one lesson's length. The system and its timings are chosen in «نظام الدوام والأوقات» above.
 */
export function ShiftsCard({ yearId }: { yearId: number }) {
  const feedback = useFormFeedback();
  const shifts = useShifts(yearId);
  const shift = shifts.data?.items[0] ?? null;
  const reload = () => { feedback.reset(); void shifts.refetch(); };
  return (
    <Card className="page-card" aria-labelledby="shifts-title">
      <SectionTitle level={2} icon={ListOrdered} id="shifts-title">{messages.school.shiftSystem.manualPeriods}</SectionTitle>
      <p className="ui-field-hint">{messages.school.shiftSystem.manualPeriodsHint}</p>
      {shifts.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {shifts.data && !shift && <p className="card-note">{text.noShifts}</p>}
      {shift && <PeriodsEditor key={`shift-${shift.id}`} yearId={yearId} shift={shift} onReload={reload} onSaved={() => feedback.showSuccess(text.periodsSaved)} generatorStart={shift.kind === "evening" ? "13:00" : "08:00"} />}
      {shift && <DayLessonsEditor key={`days-${shift.id}`} yearId={yearId} shift={shift} onReload={reload} onSaved={() => feedback.showSuccess(text.dayLessonsSaved)} />}
    </Card>
  );
}
