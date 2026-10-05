import { OrphanBlockedNotice } from "./OrphanBlockedNotice";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useYearChoice, YearPicker } from "../academic-years/YearPicker";
import { BellSettingsCard } from "./BellSettingsCard";
import { ShiftModeCard } from "./ShiftModeCard";
import { ShiftsCard } from "./ShiftsCard";
import { WorkingDaysCard } from "./WorkingDaysCard";
import { useBellSettings, useWorkingWeek } from "./scheduleApi";

const text = messages.school.scheduleStructure;

/** الدوام والحصص والجرس: working days, the selected year's shifts and periods, and bell settings. */
export function ScheduleStructurePage() {
  const choice = useYearChoice();
  const week = useWorkingWeek();
  const bells = useBellSettings();

  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} />
      <OrphanBlockedNotice />
      {(week.isError || bells.isError || choice.years.isError) && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {week.data && <WorkingDaysCard week={week.data} onReload={() => void week.refetch()} />}
      <ShiftModeCard />
      <Card className="page-card">
        <YearPicker id="structure-year" choice={choice} />
      </Card>
      {choice.yearId !== null && <ShiftsCard key={`year-${choice.yearId}`} yearId={choice.yearId} />}
      {bells.data && <BellSettingsCard settings={bells.data} onReload={() => void bells.refetch()} />}
    </div>
  );
}
