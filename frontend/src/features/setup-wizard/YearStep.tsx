import { useState } from "react";
import { DateField } from "../../components/DateField";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { ltrRuns } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { todayIn } from "../../lib/format";
import { useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useAcademicYears } from "../academic-years/yearsApi";
import { WizardFooter } from "./WizardFrame";
import { useSaveYearStep, type WizardYearInput } from "./wizardApi";
import { proposeYear, proposedStartYear, yearChoices } from "./yearProposal";

const text = messages.school.wizard;

/** Step 2: a proposed year chosen from a list, with editable dates and two proposed terms. */
export function YearStep({ onBack, onDone }: { onBack: () => void; onDone: () => void }) {
  const feedback = useFormFeedback();
  const save = useSaveYearStep();
  const context = useSchoolContext();
  const years = useAcademicYears({ search: "", page: 1, pageSize: 100 });
  const today = todayIn(context.data?.timeZone ?? "Asia/Baghdad");
  const current = years.data?.items.find((year) => year.isCurrent);
  const [draft, setDraft] = useState<WizardYearInput | null>(null);
  const saved: WizardYearInput | null = current
    ? { label: current.label, startDate: current.startDate, endDate: current.endDate, terms: current.terms.map(({ name, startDate, endDate }) => ({ name, startDate, endDate })) }
    : null;
  const value = draft ?? saved ?? proposeYear(proposedStartYear(today), text.year.termNames);
  const choices = [...new Set([...yearChoices(today).map((start) => `${start}-${start + 1}`), value.label])];

  function chooseLabel(label: string) {
    const start = Number(label.split("-")[0]);
    setDraft(Number.isNaN(start) ? { ...value, label } : proposeYear(start, text.year.termNames));
  }

  const setTerm = (index: number, key: "startDate" | "endDate", date: string) =>
    setDraft({ ...value, terms: value.terms.map((term, position) => (position === index ? { ...term, [key]: date } : term)) });

  return (
    <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={(event) => { event.preventDefault(); }}>
      <Field id="wizard-year-label" label={text.year.choose} hint={text.suggestion}>
        <Select id="wizard-year-label" value={value.label} aria-describedby="wizard-year-label-hint" onChange={(event) => chooseLabel(event.target.value)}
          options={choices.map((label) => ({ value: label, label: ltrRuns(label) }))} />
      </Field>
      <div className="form-grid">
        <DateField id="wizard-year-start" label={text.year.start} value={value.startDate} onChange={(date) => setDraft({ ...value, startDate: date })} field="StartDate" errors={feedback.fieldErrors} />
        <DateField id="wizard-year-end" label={text.year.end} value={value.endDate} onChange={(date) => setDraft({ ...value, endDate: date })} field="EndDate" errors={feedback.fieldErrors} />
      </div>
      <fieldset className="choice-group wizard-terms">
        <legend>{text.year.terms}</legend>
        {value.terms.map((term, index) => (
          <div key={`wizard-term-${term.name}`} className="form-grid">
            <DateField id={`wizard-term-${index}-start`} label={text.year.termStart(term.name)} value={term.startDate} onChange={(date) => setTerm(index, "startDate", date)} />
            <DateField id={`wizard-term-${index}-end`} label={text.year.termEnd(term.name)} value={term.endDate} onChange={(date) => setTerm(index, "endDate", date)} />
          </div>
        ))}
      </fieldset>
      <WizardFooter step={2} pending={save.isPending} error={feedback.error} onBack={onBack}
        onNext={() => { feedback.reset(); save.mutate(value, { onSuccess: onDone, onError: feedback.showError }); }} />
    </form>
  );
}
