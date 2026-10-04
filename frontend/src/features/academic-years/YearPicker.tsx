import { CalendarPlus } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { ltrRuns } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { useAcademicYears } from "./yearsApi";

/** The year a year-scoped screen works on: the owner's choice, else the current year, else the newest. */
export function useYearChoice() {
  const years = useAcademicYears({ search: "", page: 1, pageSize: 100 });
  const [chosen, setChosen] = useState<number | null>(null);
  const items = years.data?.items ?? [];
  const chosenExists = chosen !== null && items.some((year) => year.id === chosen);
  const yearId = (chosenExists ? chosen : null) ?? items.find((year) => year.isCurrent)?.id ?? items[0]?.id ?? null;
  return { years, yearId, setYearId: setChosen };
}

type YearPickerProps = {
  id: string;
  choice: ReturnType<typeof useYearChoice>;
  onChange?: () => void;
};

/** Year selector for year-scoped screens; with no year yet, an alert links to the years screen. */
export function YearPicker({ id, choice, onChange }: YearPickerProps) {
  const items = choice.years.data?.items ?? [];
  if (choice.years.isSuccess && items.length === 0) {
    return (
      <div className="year-picker-empty">
        <Alert tone="info" message={messages.school.scheduleStructure.noYear} />
        <Link className="link-button" to="/school/year">
          <CalendarPlus aria-hidden="true" size={20} />
          <span>{messages.school.scheduleStructure.goToYears}</span>
        </Link>
      </div>
    );
  }
  return (
    <Field id={id} label={messages.school.scheduleStructure.year}>
      <Select
        id={id}
        value={choice.yearId === null ? "" : String(choice.yearId)}
        options={items.map((year) => ({ value: String(year.id), label: ltrRuns(year.label) }))}
        onChange={(event) => { choice.setYearId(Number(event.target.value)); onChange?.(); }}
      />
    </Field>
  );
}
