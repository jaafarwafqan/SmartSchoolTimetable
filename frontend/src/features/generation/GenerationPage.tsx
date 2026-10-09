import { CalendarRange, ChevronDown, CircleAlert, CircleCheck, ExternalLink, Play, ShieldCheck, Square } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { Link } from "react-router-dom";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ChoiceCards } from "../../components/ui/choice-cards";
import { Field } from "../../components/ui/field";
import { Input } from "../../components/ui/input";
import { DataTable } from "../../components/ui/table";
import { LtrText } from "../../components/ui/ltr-text";
import { Select } from "../../components/ui/select";
import { Stepper } from "../../components/ui/stepper";
import { messages } from "../../i18n/messages";
import { isolate } from "../../i18n/isolate";
import { PageHeader } from "../../layout/PageHeader";
import type { Formatter } from "../../lib/format";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { errorCount, fixHref, warningCount } from "../readiness/readinessPresentation";
import { useReadiness } from "../readiness/readinessApi";
import {
  isActive, useCancelGeneration, useCurrentRun, useEngine, useRunHistory, useStartGeneration,
  type GenerationMode, type GenerationRun,
} from "./generationApi";
import { diagnosticMessage, elapsedSeconds, phaseIndex, phaseOrder, seconds, statusNote, statusTone, timeLimitChoices } from "./generationPresentation";

const text = messages.school.generation;
const readinessText = messages.school.readiness;

/** Re-renders every second while a run is active, for the real elapsed timer. */
function useNow(active: boolean): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!active) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [active]);
  return now;
}

function ProgressPanel({ run, format, onStop, stopping }: { run: GenerationRun; format: Formatter; onStop: () => void; stopping: boolean }) {
  const now = useNow(true);
  const current = phaseIndex(run);
  const elapsed = elapsedSeconds(run, now);
  const live = run.live;
  return (
    <Card className="page-card generation-progress" aria-labelledby="generation-progress-title">
      <div className="generation-heading">
        <h2 id="generation-progress-title">{text.progressTitle}</h2>
        <Button variant="danger" icon={<Square aria-hidden="true" size={18} />} loading={stopping} onClick={onStop}>{stopping ? text.stopping : text.stop}</Button>
      </div>
      <ol className="generation-steps">
        {phaseOrder.map((phase, index) => (
          <li key={phase} className={index < current ? "is-done" : index === current ? "is-current" : ""} aria-current={index === current ? "step" : undefined}>
            {index < current ? <CircleCheck aria-hidden="true" size={18} /> : <span className="generation-step-dot" aria-hidden="true" />}
            <span>{text.steps[phase]}</span>
          </li>
        ))}
      </ol>
      <dl className="generation-stats" aria-live="polite">
        <div><dt>{text.elapsed}</dt><dd>{elapsed === null ? text.none : seconds(elapsed, format)}</dd></div>
        <div><dt>{text.improvements}</dt><dd>{format.number(live?.improvements ?? 0)}</dd></div>
        <div><dt>{text.bestScore}</dt><dd>{live?.bestObjective === null || live?.bestObjective === undefined ? text.noSolutionYet : format.number(live.bestObjective)}</dd></div>
        <div><dt>{text.bound}</dt><dd>{live?.bestBound === null || live?.bestBound === undefined ? text.none : format.number(live.bestBound)}</dd></div>
        <div><dt>{text.firstSolution}</dt><dd>{live?.firstSolutionSeconds ? seconds(live.firstSolutionSeconds, format) : text.none}</dd></div>
      </dl>
      {!live?.firstSolutionSeconds && run.status === "generating" && <p className="card-note">{text.waitingForSolver}</p>}
    </Card>
  );
}

function ResultPanel({ run, format }: { run: GenerationRun; format: Formatter }) {
  const tone = statusTone(run.status);
  return (
    <Card className="page-card generation-result" aria-labelledby="generation-result-title">
      <div className="generation-heading">
        <h2 id="generation-result-title">{text.resultTitle}</h2>
        <Badge tone={tone} icon={tone === "success" ? <CircleCheck aria-hidden="true" size={16} /> : <CircleAlert aria-hidden="true" size={16} />}>
          {text.statuses[run.status]}
        </Badge>
      </div>
      <p>{statusNote(run)}</p>
      {run.errorCode && run.status === "failed" && <Alert tone="error" message={messages.errors[run.errorCode as keyof typeof messages.errors] ?? messages.errors.UNKNOWN_ERROR} />}
      <dl className="generation-stats">
        <div><dt>{text.lessonsPlaced}</dt><dd>{format.count(run.lessonsPlaced, "lesson")}</dd></div>
        {run.score && <div><dt>{text.totalScore}</dt><dd>{format.number(run.score.total)}</dd></div>}
        {run.elapsedSeconds !== null && <div><dt>{text.elapsed}</dt><dd>{seconds(run.elapsedSeconds, format)}</dd></div>}
        {run.firstSolutionSeconds !== null && <div><dt>{text.firstSolution}</dt><dd>{seconds(run.firstSolutionSeconds, format)}</dd></div>}
      </dl>
      {run.timetableVersionId && (
        <p className="generation-verified"><ShieldCheck aria-hidden="true" size={18} /><span>{text.verified}</span></p>
      )}
      {run.score && (
        <DataTable caption={text.scoreTitle} rows={run.score.rules} rowKey={(rule) => rule.key} columns={[
          { key: "rule", header: text.rule, cell: (rule) => text.rules[rule.key as keyof typeof text.rules] ?? text.unknownDiagnostic },
          { key: "penalty", header: text.penalty, numeric: true, cell: (rule) => format.number(rule.penalty) },
          { key: "weight", header: text.weight, numeric: true, cell: (rule) => (rule.enabled ? format.number(rule.weight) : text.ruleDisabled) },
          { key: "weighted", header: text.weighted, numeric: true, cell: (rule) => format.number(rule.weighted) },
        ]} />
      )}
      <p className="readiness-meta">
        <span>{text.seedUsed(format.number(run.seed))}</span>
        <span>{text.hashUsed} <LtrText>{readinessText.shortHash(run.inputHash.slice(0, 12))}</LtrText></span>
      </p>
      {run.timetableVersionId && (
        <Link className="link-button" to={`/timetable/view?version=${run.timetableVersionId}`}>
          <CalendarRange aria-hidden="true" size={18} /><span>{text.openTimetable}</span>
        </Link>
      )}
    </Card>
  );
}

function DiagnosticsPanel({ run, format }: { run: GenerationRun; format: Formatter }) {
  const diagnostics = run.diagnostics;
  if (!diagnostics || diagnostics.findings.length === 0) return null;
  const isTimeout = run.status === "timedOut";
  return (
    <Card className="page-card generation-diagnostics" aria-labelledby="generation-diagnostics-title">
      <h2 id="generation-diagnostics-title">{text.diagnosticsTitle}</h2>
      {!isTimeout && <p className="card-note">{diagnostics.minimal ? text.diagnosticsMinimal : text.diagnosticsNotMinimal}</p>}
      <ul className="readiness-finding-list">
        {diagnostics.findings.map((finding, index) => (
          <li key={`${finding.code}-${finding.entity.id}-${index}`}>
            <p>{diagnosticMessage(finding, format)}</p>
            {finding.related.length > 0 && <p className="readiness-related">{text.related} {finding.related.map((entity) => isolate(entity.name)).join("، ")}</p>}
            {finding.relaxationHelps === true && <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.relaxHelps}</Badge>}
            {finding.relaxationHelps === false && <Badge>{text.relaxNotEnough}</Badge>}
            <span className="generation-fixes">
              {finding.fixes.map((fix) => (
                <Link key={fix} className="link-button" to={fixHref(fix)}>
                  <ExternalLink aria-hidden="true" size={16} /><span>{readinessText.fixes[fix as keyof typeof readinessText.fixes] ?? readinessText.title}</span>
                </Link>
              ))}
            </span>
          </li>
        ))}
      </ul>
    </Card>
  );
}

export function GenerationPage() {
  const format = useFormatter();
  const school = useSchoolContext();
  const yearId = school.data?.currentYear?.id;
  const engine = useEngine();
  const [mode, setMode] = useState<GenerationMode>("standard");
  const [timeLimit, setTimeLimit] = useState(60);
  const [deterministic, setDeterministic] = useState(false);
  const [advanced, setAdvanced] = useState(false);
  const [workers, setWorkers] = useState<number | null>(null);
  const [seed, setSeed] = useState("");
  const readiness = useReadiness(yearId, mode === "doublePeriods");
  const current = useCurrentRun(yearId);
  const history = useRunHistory(yearId);
  const start = useStartGeneration(yearId);
  const cancel = useCancelGeneration();
  const feedback = useFormFeedback();
  const advancedId = useId();
  const run = current.data?.run ?? null;
  const active = isActive(run);
  const seedValue = seed.trim() === "" ? null : Number(seed.trim());
  const seedInvalid = seedValue !== null && (!Number.isInteger(seedValue) || seedValue < 0 || seedValue > 2147483647);
  const engineReady = engine.data?.available === true;
  const canStart = Boolean(yearId) && engineReady && readiness.data?.ready === true && !active && !seedInvalid;

  // A finished run refreshes the readiness and the saved versions it may have created.
  useEffect(() => {
    if (run && !active) void history.refetch();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [run?.status]);

  function onStart() {
    feedback.reset();
    start.mutate(
      { mode, timeLimitSeconds: timeLimit, deterministic, seed: seedValue, workers: workers ?? engine.data?.defaultWorkers ?? 1 },
      { onSuccess: () => feedback.showSuccess(text.started), onError: feedback.showError },
    );
  }

  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} />
      {!yearId && school.isSuccess && <Alert tone="warning" message={readinessText.noYear} />}
      {engine.data && !engineReady && <Alert tone="error" message={messages.errors.SOLVER_UNAVAILABLE} />}
      {current.isError && <Alert tone="error" message={text.loadFailed}>{userErrorMessage(current.error)}</Alert>}
      <Alert tone="error" message={feedback.error} />
      <Alert tone="success" message={feedback.success} />

      {yearId && (
        <Card className="page-card readiness-summary" aria-labelledby="generation-readiness-title">
          <div className="generation-heading">
            <h2 id="generation-readiness-title">{text.readinessTitle}</h2>
            <Link className="link-button" to="/readiness"><ExternalLink aria-hidden="true" size={16} /><span>{text.openReadiness}</span></Link>
          </div>
          {readiness.isPending && <p>{readinessText.loading}</p>}
          {readiness.isError && <Alert tone="error" message={readinessText.loadFailed}>{userErrorMessage(readiness.error)}</Alert>}
          {readiness.data && (
            <>
              <p className={`readiness-status${readiness.data.ready ? " is-ready" : " has-errors"}`}>
                {readiness.data.ready ? <CircleCheck aria-hidden="true" size={22} /> : <CircleAlert aria-hidden="true" size={22} />}
                <strong>{readiness.data.ready ? text.readinessReady : text.readinessBlocked}</strong>
              </p>
              <div className="readiness-counts">
                <Badge tone={readiness.data.errors ? "danger" : "success"}>{errorCount(readiness.data.errors, format)}</Badge>
                <Badge>{warningCount(readiness.data.warnings, format)}</Badge>
              </div>
            </>
          )}
        </Card>
      )}

      {yearId && !active && (
        <Card className="page-card generation-options" aria-labelledby="generation-options-title">
          <h2 id="generation-options-title">{text.optionsTitle}</h2>
          <ChoiceCards<GenerationMode>
            name="generation-mode"
            legend={text.mode}
            value={mode}
            onChange={setMode}
            choices={[
              { value: "standard", label: text.modes.standard, description: text.modeHints.standard },
              { value: "doublePeriods", label: text.modes.doublePeriods, description: text.modeHints.doublePeriods },
            ]}
          />
          <Field id="generation-time-limit" label={text.timeLimit} hint={text.timeLimitHint}>
            <Select id="generation-time-limit" value={String(timeLimit)} aria-describedby="generation-time-limit-hint"
              onChange={(event) => setTimeLimit(Number(event.target.value))}
              options={timeLimitChoices.map((value) => ({ value: String(value), label: seconds(value, format) }))} />
          </Field>
          <span className="form-stack">
            <Checkbox checked={deterministic} onChange={(event) => setDeterministic(event.target.checked)}>{text.deterministic}</Checkbox>
            <span className="card-note">{text.deterministicHint}</span>
          </span>
          <div>
            <Button variant="ghost" className="generation-advanced-toggle" aria-expanded={advanced} aria-controls={advancedId} onClick={() => setAdvanced((open) => !open)}
              icon={<ChevronDown aria-hidden="true" size={18} className={advanced ? "is-open" : undefined} />}>{text.advanced}</Button>
          </div>
          {advanced && (
            <div id={advancedId} className="generation-advanced">
              {!deterministic && engine.data && (
                <Field id="generation-workers" label={text.workers}>
                  <Stepper id="generation-workers" label={text.workers} value={workers ?? engine.data.defaultWorkers} min={1} max={engine.data.maxWorkers}
                    format={format.number} decreaseLabel={text.decreaseWorkers} increaseLabel={text.increaseWorkers} onChange={setWorkers} />
                </Field>
              )}
              <Field id="generation-seed" label={text.seed} hint={text.seedHint} error={seedInvalid ? messages.errors.VALUE_OUT_OF_RANGE : undefined}>
                <Input id="generation-seed" className="generation-seed" inputMode="numeric" dir="ltr" value={seed} autoComplete="off"
                  aria-invalid={seedInvalid || undefined} aria-describedby={seedInvalid ? "generation-seed-error" : "generation-seed-hint"}
                  onChange={(event) => setSeed(event.target.value)} />
              </Field>
            </div>
          )}
          <div>
            <Button icon={<Play aria-hidden="true" size={18} />} disabled={!canStart} loading={start.isPending} onClick={onStart}>{text.start}</Button>
          </div>
          {engine.data?.version && <p className="card-note"><LtrText>{text.engineVersion(engine.data.version)}</LtrText></p>}
        </Card>
      )}

      {run && active && (
        <ProgressPanel run={run} format={format} stopping={cancel.isPending}
          onStop={() => cancel.mutate(run.id, { onSuccess: () => feedback.showSuccess(text.stopRequested), onError: feedback.showError })} />
      )}
      {run && !active && <ResultPanel run={run} format={format} />}
      {run && !active && <DiagnosticsPanel run={run} format={format} />}

      {history.data && history.data.items.length > 1 && (
        <Card className="page-card" aria-labelledby="generation-history-title">
          <h2 id="generation-history-title">{text.history}</h2>
          <DataTable caption={text.history} rows={history.data.items} rowKey={(item) => String(item.id)} columns={[
            { key: "date", header: text.startedAt, cell: (item) => format.date(item.queuedAt.slice(0, 10)) },
            { key: "mode", header: text.mode, cell: (item) => text.modes[item.mode] },
            { key: "status", header: text.resultTitle, cell: (item) => <Badge tone={statusTone(item.status)}>{text.statuses[item.status]}</Badge> },
            { key: "score", header: text.totalScore, numeric: true, cell: (item) => (item.score ? format.number(item.score.total) : text.none) },
          ]} />

        </Card>
      )}
    </div>
  );
}
