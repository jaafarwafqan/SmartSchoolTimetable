import { CalendarRange, Check, CircleAlert, CircleCheck, CircleSlash, Hand, Sparkles } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { weekdayLabel } from "../timetable-structure/weekdays";
import { useApplyDailySuggestion, useDailySuggestion, type DailyStage, type DailyStatus } from "./curriculumApi";

const text = messages.school.daily;

const looks: Record<DailyStatus, { tone: "primary" | "success" | "neutral" | "danger" | "warning"; icon: ReactNode }> = {
  apply: { tone: "primary", icon: <Sparkles aria-hidden="true" size={14} /> },
  same: { tone: "success", icon: <CircleCheck aria-hidden="true" size={14} /> },
  manual: { tone: "neutral", icon: <Hand aria-hidden="true" size={14} /> },
  aboveCapacity: { tone: "danger", icon: <CircleAlert aria-hidden="true" size={14} /> },
  belowDays: { tone: "warning", icon: <CircleAlert aria-hidden="true" size={14} /> },
  noCurriculum: { tone: "neutral", icon: <CircleSlash aria-hidden="true" size={14} /> },
};

/**
 * "اقتراح توزيع الحصص اليومية" (ADR 0030): each stage's daily lessons derived from its curriculum total, applied only to
 * the stages the owner selects and confirms. Stages edited by hand are shown but never changed.
 */
export function DailySuggestionPanel({ yearId }: { yearId: number }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const suggestion = useDailySuggestion(yearId);
  const apply = useApplyDailySuggestion(yearId);
  const [unselected, setUnselected] = useState<ReadonlySet<number>>(new Set());
  const [confirming, setConfirming] = useState(false);
  const stages = suggestion.data?.stages ?? [];
  const selectable = stages.filter((stage) => stage.status === "apply");
  const selected = selectable.filter((stage) => !unselected.has(stage.stageId));

  const toggle = (stageId: number) => setUnselected((current) => {
    const next = new Set(current);
    if (!next.delete(stageId)) next.add(stageId);
    return next;
  });
  const days = (stage: DailyStage) => stage.suggested.map((day) => format.number(day.lessons)).join(text.separator);
  const dayNames = (stage: DailyStage) => stage.suggested.map((day) => `${weekdayLabel(day.day)} ${format.number(day.lessons)}`).join(text.separator);

  const columns: readonly TableColumn<DailyStage>[] = [
    {
      key: "choose",
      header: text.status,
      cell: (stage) => stage.status === "apply"
        ? <Checkbox checked={!unselected.has(stage.stageId)} onChange={() => toggle(stage.stageId)}>{text.statuses.apply}<span className="sr-only">{text.choose(stage.stageName)}</span></Checkbox>
        : <Badge tone={looks[stage.status].tone} icon={looks[stage.status].icon}>{text.statuses[stage.status]}</Badge>,
    },
    { key: "stage", header: text.stage, cell: (stage) => stage.stageName },
    { key: "total", header: text.total, cell: (stage) => format.count(stage.weeklyTotal, "lesson"), numeric: true },
    {
      key: "days",
      header: text.days,
      cell: (stage) => (
        <span className="daily-days">
          {stage.suggested.length > 0 && <span title={dayNames(stage)}>{days(stage)}</span>}
          {stage.status === "aboveCapacity" && <span className="card-note">{text.aboveHint(format.count(stage.weeklyTotal, "lesson"), format.count(stage.capacity, "lesson"))}</span>}
          {stage.changedSinceSuggestion && <span className="daily-changed"><CircleAlert aria-hidden="true" size={14} />{text.changed}</span>}
        </span>
      ),
    },
  ];

  return (
    <Card className="page-card" aria-labelledby="daily-suggestion-title">
      <h2 id="daily-suggestion-title" className="tool-title"><CalendarRange aria-hidden="true" size={22} />{text.title}</h2>
      <p className="card-note">{text.description}</p>
      {suggestion.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <div className="daily-table">
        <DataTable caption={text.title} columns={columns} rows={stages} rowKey={(stage) => `daily-${stage.stageId}`} loading={suggestion.isPending} scrollable />
      </div>
      {suggestion.isSuccess && selectable.length === 0 && <p className="card-note">{text.none}</p>}
      {selectable.length > 0 && (
        <div className="form-actions">
          <Button icon={<Check aria-hidden="true" size={20} />} disabled={selected.length === 0} loading={apply.isPending}
            onClick={() => { feedback.reset(); setConfirming(true); }}>{text.apply}</Button>
        </div>
      )}
      <ConfirmDialog
        open={confirming}
        title={text.confirmTitle}
        consequence={text.confirmConsequence(format.count(selected.length, "stage", "oblique"))}
        confirmLabel={text.apply}
        confirmIcon={<Check aria-hidden="true" size={20} />}
        loading={apply.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() => apply.mutate(selected.map((stage) => stage.stageId), {
          onSuccess: () => { setConfirming(false); setUnselected(new Set()); feedback.showSuccess(text.applied); },
          onError: (reason) => { setConfirming(false); feedback.showError(reason); },
        })}
      />
    </Card>
  );
}
