import { CalendarRange, ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Select } from "../../components/ui/select";
import { SectionTitle } from "../../components/ui/section-title";
import { Spinner } from "../../components/ui/spinner";
import { DataTable } from "../../components/ui/table";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSections, useStages, type Stage } from "../stages-sections/stagesApi";
import { dailyCapChoices, useSetCapOverride, useSetDailyCap, useSubjects, type CapScope, type Subject } from "../subjects/subjectsApi";

const text = messages.school.schedulingProfile.dailyCap;

type CapSelectProps = {
  id: string;
  label: string;
  value: number | null;
  /** What «تلقائي» means here: the limit this level inherits from the one above it, when that one is set. */
  inherited: number | null;
  disabled: boolean;
  onChange: (cap: number | null) => void;
};

/** One choice of the daily limit: «تلقائي» (inherit) or 1 to 6 lessons a day. Chosen, never typed. */
function CapSelect({ id, label, value, inherited, disabled, onChange }: CapSelectProps) {
  const format = useFormatter();
  const describe = (cap: number) => (cap === 1 ? text.oneLesson : text.lessonsPerDay(format.count(cap, "lesson")));
  return (
    <Select id={id} aria-label={label} title={label} disabled={disabled} value={value === null ? "" : String(value)}
      options={[
        { value: "", label: inherited === null ? text.auto : text.inherits(describe(inherited)) },
        ...dailyCapChoices.map((cap) => ({ value: String(cap), label: describe(cap) })),
      ]}
      onChange={(event) => onChange(event.target.value === "" ? null : Number(event.target.value))} />
  );
}

const overrideOf = (subject: Subject, scope: CapScope, scopeId: number): number | null =>
  subject.capOverrides?.find((item) => item.scope === scope && item.scopeId === scopeId)?.cap ?? null;

type SetOverride = (scope: CapScope, scopeId: number, cap: number | null) => void;

function StageCaps({ yearId, subject, stage, disabled, onSet }: { yearId: number; subject: Subject; stage: Stage; disabled: boolean; onSet: SetOverride }) {
  const sections = useSections(yearId, stage.id, false);
  const stageCap = overrideOf(subject, "stage", stage.id);
  const stageName = isolate(stage.name);
  return (
    <li className="cap-stage">
      <div className="profile-rule">
        <span className="profile-rule-text"><strong>{text.stageRow(stageName)}</strong></span>
        <CapSelect id={`cap-${subject.id}-stage-${stage.id}`} label={text.chooseForStage(isolate(subject.name), stageName)} value={stageCap}
          inherited={subject.dailyCap ?? null} disabled={disabled} onChange={(cap) => onSet("stage", stage.id, cap)} />
      </div>
      {sections.isPending && <Spinner label={messages.app.loadingContent} />}
      {sections.data && sections.data.items.length === 0 && <p className="card-note">{text.noSections}</p>}
      <ul className="profile-rules cap-sections">
        {(sections.data?.items ?? []).map((section) => (
          <li key={section.id} className="profile-rule">
            <span className="profile-rule-text">{text.sectionRow(isolate(section.label))}</span>
            <CapSelect id={`cap-${subject.id}-section-${section.id}`} label={text.chooseForSection(isolate(subject.name), isolate(`${stage.name} ${section.label}`))}
              value={overrideOf(subject, "section", section.id)} inherited={stageCap ?? subject.dailyCap ?? null} disabled={disabled}
              onChange={(cap) => onSet("section", section.id, cap)} />
          </li>
        ))}
      </ul>
    </li>
  );
}

/**
 * The owner's daily limit of each subject (H9) at three levels: the subject, a grade, a section; the most specific wins.
 * An inline table of choices («تلقائي» inherits from the level above); the grade and section levels open under the subject.
 */
export function DailyCapCard() {
  const feedback = useFormFeedback();
  const school = useSchoolContext();
  const yearId = school.data?.currentYear?.id ?? 0;
  const subjects = useSubjects({ search: "", page: 1, pageSize: 100, includeArchived: false });
  const stages = useStages(yearId, "", false);
  const saveDefault = useSetDailyCap();
  const saveOverride = useSetCapOverride();
  const [open, setOpen] = useState<number | null>(null);
  const selected = subjects.data?.items.find((subject) => subject.id === open) ?? null;
  const format = useFormatter();
  const busy = saveDefault.isPending || saveOverride.isPending;
  const done = { onSuccess: () => feedback.showSuccess(text.saved), onError: feedback.showError };
  return (
    <Card className="page-card" aria-labelledby="daily-cap-title">
      <SectionTitle level={2} icon={CalendarRange} id="daily-cap-title">{text.title}</SectionTitle>
      <p className="card-note">{text.description}</p>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {subjects.isPending && <Spinner label={messages.app.loadingContent} />}
      {subjects.data && subjects.data.items.length === 0 && <p className="card-note">{text.empty}</p>}
      {subjects.data && subjects.data.items.length > 0 && (
        <DataTable caption={text.title} rows={subjects.data.items} rowKey={(subject) => String(subject.id)} selectedKey={open === null ? null : String(open)} columns={[
          { key: "subject", header: text.subject, cell: (subject) => <strong>{isolate(subject.name)}</strong> },
          {
            key: "default", header: text.subjectDefault,
            cell: (subject) => (
              <CapSelect id={`daily-cap-${subject.id}`} label={text.chooseFor(isolate(subject.name))} value={subject.dailyCap ?? null} inherited={null} disabled={busy}
                onChange={(cap) => { feedback.reset(); saveDefault.mutate({ subject, dailyCap: cap }, done); }} />
            ),
          },
          {
            key: "details", header: text.details,
            cell: (subject) => {
              const expanded = open === subject.id;
              const count = subject.capOverrides?.length ?? 0;
              return (
                <Button variant="ghost" size="sm" icon={expanded ? <ChevronUp aria-hidden="true" size={18} /> : <ChevronDown aria-hidden="true" size={18} />}
                  aria-expanded={expanded} aria-controls="cap-details" onClick={() => setOpen(expanded ? null : subject.id)}>
                  {expanded ? text.detailsOpen : text.details}
                  {count > 0 && <> {text.overridesCount(format.number(count))}</>}
                </Button>
              );
            },
          },
        ]} />
      )}
      <div id="cap-details" aria-live="polite">
        {selected && (
          <section className="form-stack" aria-labelledby="cap-details-title">
            <SectionTitle level={3} icon={CalendarRange} id="cap-details-title">{text.detailsTitle(isolate(selected.name))}</SectionTitle>
            {stages.isPending && <Spinner label={messages.app.loadingContent} />}
            {stages.data && stages.data.items.length === 0 && <p className="card-note">{text.noStages}</p>}
            <ul className="profile-rules cap-stages" aria-label={text.stageHeading}>
              {(stages.data?.items ?? []).map((stage) => (
                <StageCaps key={stage.id} yearId={yearId} subject={selected} stage={stage} disabled={busy}
                  onSet={(scope, scopeId, cap) => { feedback.reset(); saveOverride.mutate({ subject: selected, scope, scopeId, cap }, done); }} />
              ))}
            </ul>
          </section>
        )}
      </div>
    </Card>
  );
}
