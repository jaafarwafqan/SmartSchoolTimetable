import { CircleAlert, CircleCheck, Filter, Sparkles, TriangleAlert, UsersRound } from "lucide-react";
import { useState } from "react";
import { LoadStatusBadge } from "../../components/LoadBar";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Checkbox } from "../../components/ui/checkbox";
import { EmptyState } from "../../components/ui/empty-state";
import { Field } from "../../components/ui/field";
import { SectionTitle } from "../../components/ui/section-title";
import { Select } from "../../components/ui/select";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { AssignmentSuggester } from "./AssignmentSuggester";
import { teacherChoices, useSetWorkloadCell, useTeacherLoads, useWorkloadRows, type WorkloadRow } from "./workloadApi";
import { filterRows, rowStatus, totals, unassignedFilter, type RowStatus } from "./workloadRows";

const text = messages.school.workloadTable;

function StatusBadge({ status }: { status: RowStatus }) {
  if (status === "assigned") return <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.statuses.assigned}</Badge>;
  if (status === "overloaded") return <Badge tone="warning" icon={<TriangleAlert aria-hidden="true" size={16} />}>{text.statuses.overloaded}</Badge>;
  return <Badge tone="danger" icon={<CircleAlert aria-hidden="true" size={16} />}>{text.statuses.missing}</Badge>;
}

/**
 * MF2 «الأنصبة»: one clear table of every section × subject with a teacher chosen per row (choose, don't type), filters by stage
 * and teacher, a status per row (icon and text), «اقتراح تلقائي» for the rows without a teacher only, and each teacher's load.
 */
export function WorkloadTable({ yearId }: { yearId: number }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const rows = useWorkloadRows(yearId);
  const loads = useTeacherLoads(yearId);
  const setCell = useSetWorkloadCell(yearId);
  const [stageId, setStageId] = useState<number | null>(null);
  const [teacherId, setTeacherId] = useState<number | null>(null);
  const [showAll, setShowAll] = useState(false);
  const all = rows.data ?? [];
  const teacherLoads = loads.data ?? [];
  const shown = filterRows(all, { stageId, teacherId });
  const count = totals(all);
  const stages = [...new Map(all.map((row) => [row.stageId, row.stageName])).entries()];

  function choose(row: WorkloadRow, value: string) {
    feedback.reset();
    const next = value === "" ? null : Number(value);
    setCell.mutate({ sectionId: row.sectionId, entryId: row.entryId, teacherId: next, assignmentId: row.assignmentId, version: row.version },
      { onSuccess: () => feedback.showSuccess(text.saved), onError: feedback.showError });
  }

  const columns: TableColumn<WorkloadRow>[] = [
    { key: "section", header: text.section, cell: (row) => text.sectionName(row.stageName, row.sectionLabel) },
    { key: "subject", header: text.subject, cell: (row) => (row.label ? `${row.subjectName} - ${row.label}` : row.subjectName) },
    { key: "lessons", header: text.lessons, numeric: true, cell: (row) => format.number(row.weeklyLessons) },
    {
      key: "teacher", header: text.teacher,
      cell: (row) => {
        const id = `workload-row-${row.sectionId}-${row.entryId}`;
        const options = teacherChoices(teacherLoads, row.subjectId, row.teacherId, showAll);
        return (
          <Select id={id} aria-label={text.chooseTeacher(row.subjectName, text.sectionName(row.stageName, row.sectionLabel))} value={row.teacherId === null ? "" : String(row.teacherId)}
            disabled={setCell.isPending} onChange={(event) => choose(row, event.target.value)}
            options={[{ value: "", label: text.noTeacher },
              ...options.map((item) => ({ value: String(item.teacherId), label: text.teacherOption(item.fullName, format.number(item.assignedLessons), format.number(item.limit)) }))]} />
        );
      },
    },
    {
      key: "status", header: text.status,
      cell: (row) => (
        <span className="row-actions">
          <StatusBadge status={rowStatus(row, teacherLoads)} />
          {row.outsideSpecialization && <Badge tone="warning" icon={<TriangleAlert aria-hidden="true" size={16} />}>{text.outside}</Badge>}
        </span>
      ),
    },
  ];

  return (
    <div className="form-stack workload-table">
      {(rows.isError || loads.isError) && <Alert tone="error" message={text.loadFailed} />}
      {rows.data && <p className="workload-summary">{text.summary(format.number(count.assignedRows), format.number(count.rows), format.number(count.assignedLessons), format.number(count.lessons))}</p>}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {rows.data && all.length === 0 ? <EmptyState icon={<UsersRound aria-hidden="true" size={24} />} message={text.noRows} /> : (
        <>
          <fieldset className="workload-filters">
            <legend><Filter aria-hidden="true" size={16} /><span>{text.filters}</span></legend>
            <Field id="workload-stage-filter" label={text.stageFilter}>
              <Select id="workload-stage-filter" value={stageId === null ? "" : String(stageId)} onChange={(event) => setStageId(event.target.value === "" ? null : Number(event.target.value))}
                options={[{ value: "", label: text.allStages }, ...stages.map(([id, name]) => ({ value: String(id), label: name }))]} />
            </Field>
            <Field id="workload-teacher-filter" label={text.teacherFilter}>
              <Select id="workload-teacher-filter" value={teacherId === null ? "" : String(teacherId)} onChange={(event) => setTeacherId(event.target.value === "" ? null : Number(event.target.value))}
                options={[{ value: "", label: text.allTeachers }, { value: String(unassignedFilter), label: text.unassigned },
                  ...teacherLoads.map((item) => ({ value: String(item.teacherId), label: item.fullName }))]} />
            </Field>
            <Checkbox checked={showAll} onChange={(event) => setShowAll(event.target.checked)}>{text.showAll}</Checkbox>
          </fieldset>
          <DataTable caption={text.table} columns={columns} rows={shown} rowKey={(row) => `${row.sectionId}-${row.entryId}`} loading={rows.isPending} scrollable />
        </>
      )}
      <details className="advanced-options tool-panel">
        <summary><Sparkles aria-hidden="true" size={18} /><span>{text.suggestTitle}</span></summary>
        <p className="card-note">{text.suggestHint}</p>
        <AssignmentSuggester yearId={yearId} embedded />
      </details>
      <section aria-labelledby="workload-loads-title">
        <SectionTitle level={3} icon={UsersRound} id="workload-loads-title">{text.loadsTitle}</SectionTitle>
        {loads.data && teacherLoads.length === 0 ? <p className="card-note">{text.noTeachers}</p> : (
          <DataTable caption={text.loadsTitle} rows={teacherLoads} rowKey={(item) => String(item.teacherId)} loading={loads.isPending} scrollable columns={[
            { key: "name", header: text.teacher, cell: (item) => item.fullName },
            { key: "load", header: text.loadColumn, numeric: true, cell: (item) => text.loadValue(format.number(item.assignedLessons), format.number(item.limit)) },
            { key: "status", header: text.status, cell: (item) => <LoadStatusBadge status={item.status} /> },
          ]} />
        )}
      </section>
    </div>
  );
}
