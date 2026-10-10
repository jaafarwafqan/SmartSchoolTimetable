import { ArrowLeftRight, Check, Eye, UserMinus, UserPlus, UsersRound } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useBulkPlan, useWorkloadMatrix, type BulkInput, type TeacherLoad, type WorkloadPlan, type WorkloadStageSummary } from "./workloadApi";

const text = messages.school.workload;
const bulk = text.bulk;

type Format = ReturnType<typeof useFormatter>;

/** The preview of one bulk action: every line with its action, then each affected teacher's load before and after. */
export function PlanView({ plan, format }: { plan: WorkloadPlan; format: Format }) {
  if (plan.changes === 0) return <Alert tone="info" message={bulk.nothing} />;
  return (
    <div className="form-stack">
      <ul className="reference-list plan-lines">
        {plan.lines.map((line) => (
          <li key={`${line.sectionId}-${line.entryId}`}>
            <Badge tone={line.action === "skip" || line.action === "unchanged" ? "neutral" : line.action === "remove" ? "danger" : "primary"}>{bulk.actions[line.action]}</Badge>
            {" "}
            {bulk.lineText(bulk.sectionOption(line.stageName, line.sectionLabel), line.label ? `${line.subjectName} - ${line.label}` : line.subjectName, format.number(line.weeklyLessons))}
            {line.action !== "create" && line.action !== "remove" && line.currentTeacher && line.newTeacher && line.currentTeacher !== line.newTeacher && (
              <span className="card-note"> {bulk.teacherChange(line.currentTeacher, line.newTeacher)}</span>
            )}
          </li>
        ))}
      </ul>
      {plan.loads.length > 0 && (
        <div>
          <strong>{bulk.loadsTitle}</strong>
          <ul className="reference-list">
            {plan.loads.map((load) => (
              <li key={load.teacherId}>{bulk.loadChange(load.fullName, format.number(load.before), format.number(load.after), format.number(load.limit))}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

type ToolProps = { title: string; icon: ReactNode; ready: boolean; build: () => BulkInput | null; yearId: number; children: ReactNode };

/** One bulk tool: its fields, «معاينة», the plan, then «تطبيق» after a confirmation. */
function BulkTool({ title, icon, ready, build, yearId, children }: ToolProps) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const { preview, apply } = useBulkPlan(yearId);
  const [plan, setPlan] = useState<{ input: BulkInput; result: WorkloadPlan } | null>(null);
  const [confirming, setConfirming] = useState(false);

  function showPreview() {
    const input = build();
    if (!input) return;
    feedback.reset();
    preview.mutate(input, { onSuccess: (result) => setPlan({ input, result }), onError: feedback.showError });
  }

  return (
    <details className="advanced-options tool-panel">
      <summary>{icon}<span>{title}</span></summary>
      <div className="form-stack">
        {children}
        <div className="form-actions">
          <Button variant="secondary" icon={<Eye aria-hidden="true" size={20} />} disabled={!ready} loading={preview.isPending} onClick={showPreview}>{bulk.preview}</Button>
        </div>
        <Alert tone="success" message={feedback.success} />
        <Alert tone="error" message={feedback.error} />
        {plan && <PlanView plan={plan.result} format={format} />}
        {plan && plan.result.changes > 0 && (
          <div className="form-actions">
            <Button icon={<Check aria-hidden="true" size={20} />} onClick={() => setConfirming(true)}>{bulk.apply}</Button>
          </div>
        )}
      </div>
      <ConfirmDialog
        open={confirming && plan !== null}
        title={bulk.confirmTitle}
        consequence={bulk.confirmConsequence(format.count(plan?.result.changes ?? 0, "change"))}
        confirmLabel={bulk.apply}
        confirmIcon={<Check aria-hidden="true" size={20} />}
        loading={apply.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() => plan && apply.mutate(plan.input, {
          onSuccess: (result) => { setPlan(null); feedback.showSuccess(result.changes === 0 ? bulk.nothing : bulk.applied(format.count(result.changes, "change"))); },
          onError: feedback.showError,
          onSettled: () => setConfirming(false),
        })}
      />
    </details>
  );
}

function TeacherSelect({ id, label, loads, value, onChange }: { id: string; label: string; loads: readonly TeacherLoad[]; value: number | null; onChange: (value: number | null) => void }) {
  const options = [{ value: "", label: bulk.choose }, ...loads.map((load) => ({ value: String(load.teacherId), label: load.fullName }))];
  return (
    <Field id={id} label={label}>
      <Select id={id} value={value === null ? "" : String(value)} options={options} onChange={(event) => onChange(event.target.value ? Number(event.target.value) : null)} />
    </Field>
  );
}

function StageSelect({ id, stages, value, onChange }: { id: string; stages: readonly WorkloadStageSummary[]; value: number | null; onChange: (value: number) => void }) {
  const options = [{ value: "", label: bulk.choose }, ...stages.map((stage) => ({ value: String(stage.stageId), label: stage.stageName }))];
  return (
    <Field id={id} label={text.stage}>
      <Select id={id} value={value === null ? "" : String(value)} options={options} onChange={(event) => onChange(Number(event.target.value))} />
    </Field>
  );
}

/** «إجراءات جماعية» (Phase 3 §4): previewed, non-overwriting unless chosen. */
export function BulkActions({ yearId, loads, stages }: { yearId: number; loads: readonly TeacherLoad[]; stages: readonly WorkloadStageSummary[] }) {
  const [acrossStage, setAcrossStage] = useState<number | null>(null);
  const [across, setAcross] = useState<{ teacherId: number | null; entryId: number | null; overwrite: boolean }>({ teacherId: null, entryId: null, overwrite: false });
  const [classStage, setClassStage] = useState<number | null>(null);
  const [classTeacher, setClassTeacher] = useState<{ teacherId: number | null; sectionId: number | null; overwrite: boolean }>({ teacherId: null, sectionId: null, overwrite: false });
  const [transfer, setTransfer] = useState<{ from: number | null; to: number | null }>({ from: null, to: null });
  const [remove, setRemove] = useState<number | null>(null);
  const acrossMatrix = useWorkloadMatrix(acrossStage === null ? null : yearId, acrossStage);
  const classMatrix = useWorkloadMatrix(classStage === null ? null : yearId, classStage);
  // Only the chosen stage's options: while another stage loads, the query keeps the previous stage's data, and a
  // quick choice would otherwise pick a section or line of the wrong stage.
  const acrossLoaded = acrossMatrix.data?.stage?.stageId === acrossStage ? acrossMatrix.data.stage : null;
  const classLoaded = classMatrix.data?.stage?.stageId === classStage ? classMatrix.data.stage : null;
  const lines = acrossLoaded?.lines ?? [];
  const sections = classLoaded?.sections ?? [];
  const format = useFormatter();

  return (
    <Card className="page-card" aria-labelledby="workload-bulk-title">
      <h2 id="workload-bulk-title">{bulk.title}</h2>
      <p className="card-note">{bulk.description}</p>
      <BulkTool yearId={yearId} title={bulk.acrossStage} icon={<UsersRound aria-hidden="true" size={20} />}
        ready={across.teacherId !== null && across.entryId !== null}
        build={() => (across.teacherId !== null && across.entryId !== null ? { kind: "across-stage", body: { teacherId: across.teacherId, entryId: across.entryId, overwrite: across.overwrite } } : null)}>
        <div className="form-grid">
          <TeacherSelect id="bulk-across-teacher" label={bulk.teacher} loads={loads} value={across.teacherId} onChange={(teacherId) => setAcross({ ...across, teacherId })} />
          <StageSelect id="bulk-across-stage" stages={stages} value={acrossStage} onChange={(stageId) => { setAcrossStage(stageId); setAcross({ ...across, entryId: null }); }} />
          <Field id="bulk-across-line" label={bulk.line}>
            <Select id="bulk-across-line" value={across.entryId === null ? "" : String(across.entryId)} disabled={lines.length === 0}
              options={[{ value: "", label: bulk.choose }, ...lines.map((line) => ({ value: String(line.entryId), label: text.lineHeader(line.subjectName, line.label, format.number(line.weeklyLessons)) }))]}
              onChange={(event) => setAcross({ ...across, entryId: event.target.value ? Number(event.target.value) : null })} />
          </Field>
        </div>
        <Checkbox checked={across.overwrite} onChange={(event) => setAcross({ ...across, overwrite: event.target.checked })}>{bulk.overwrite}</Checkbox>
      </BulkTool>
      <BulkTool yearId={yearId} title={bulk.classTeacher} icon={<UserPlus aria-hidden="true" size={20} />}
        ready={classTeacher.teacherId !== null && classTeacher.sectionId !== null}
        build={() => (classTeacher.teacherId !== null && classTeacher.sectionId !== null
          ? { kind: "class-teacher", body: { teacherId: classTeacher.teacherId, sectionId: classTeacher.sectionId, entryIds: [], overwrite: classTeacher.overwrite } }
          : null)}>
        <div className="form-grid">
          <TeacherSelect id="bulk-class-teacher" label={bulk.teacher} loads={loads} value={classTeacher.teacherId} onChange={(teacherId) => setClassTeacher({ ...classTeacher, teacherId })} />
          <StageSelect id="bulk-class-stage" stages={stages} value={classStage} onChange={(stageId) => { setClassStage(stageId); setClassTeacher({ ...classTeacher, sectionId: null }); }} />
          <Field id="bulk-class-section" label={bulk.sectionChoice}>
            <Select id="bulk-class-section" value={classTeacher.sectionId === null ? "" : String(classTeacher.sectionId)} disabled={sections.length === 0}
              options={[{ value: "", label: bulk.choose }, ...sections.map((section) => ({ value: String(section.sectionId), label: section.label }))]}
              onChange={(event) => setClassTeacher({ ...classTeacher, sectionId: event.target.value ? Number(event.target.value) : null })} />
          </Field>
        </div>
        <Checkbox checked={classTeacher.overwrite} onChange={(event) => setClassTeacher({ ...classTeacher, overwrite: event.target.checked })}>{bulk.overwrite}</Checkbox>
      </BulkTool>
      <BulkTool yearId={yearId} title={bulk.transfer} icon={<ArrowLeftRight aria-hidden="true" size={20} />}
        ready={transfer.from !== null && transfer.to !== null && transfer.from !== transfer.to}
        build={() => (transfer.from !== null && transfer.to !== null ? { kind: "transfer", body: { fromTeacherId: transfer.from, toTeacherId: transfer.to } } : null)}>
        <div className="form-grid">
          <TeacherSelect id="bulk-transfer-from" label={bulk.fromTeacher} loads={loads} value={transfer.from} onChange={(from) => setTransfer({ ...transfer, from })} />
          <TeacherSelect id="bulk-transfer-to" label={bulk.toTeacher} loads={loads} value={transfer.to} onChange={(to) => setTransfer({ ...transfer, to })} />
        </div>
      </BulkTool>
      <BulkTool yearId={yearId} title={bulk.remove} icon={<UserMinus aria-hidden="true" size={20} />}
        ready={remove !== null}
        build={() => (remove !== null ? { kind: "remove", body: { teacherId: remove } } : null)}>
        <TeacherSelect id="bulk-remove-teacher" label={bulk.teacher} loads={loads} value={remove} onChange={setRemove} />
      </BulkTool>
    </Card>
  );
}
