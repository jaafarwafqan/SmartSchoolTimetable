import { CircleDashed, Plus, TriangleAlert } from "lucide-react";
import { useRef, useState, type KeyboardEvent } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { EditGrid } from "../../components/ui/edit-grid";
import { EmptyState } from "../../components/ui/empty-state";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import {
  teacherChoices, useAddSpecialization, useSetWorkloadCell, useWorkloadMatrix,
  type TeacherLoad, type WorkloadCell, type WorkloadLine, type WorkloadSectionRow,
} from "./workloadApi";

const text = messages.school.workload;

type CellProps = {
  rowIndex: number;
  colIndex: number;
  line: WorkloadLine;
  row: WorkloadSectionRow;
  cell: WorkloadCell;
  loads: readonly TeacherLoad[];
  showAll: boolean;
  saving: boolean;
  onAssign: (cell: WorkloadCell, row: WorkloadSectionRow, teacherId: number | null) => void;
  onAddSpecialization: (teacher: TeacherLoad, line: WorkloadLine) => void;
};

/** One section × line cell: a teacher chooser, «غير معيّن» when empty, «خارج التخصص» with a one-click fix. */
function AssignmentCell({ rowIndex, colIndex, line, row, cell, loads, showAll, saving, onAssign, onAddSpecialization }: CellProps) {
  const format = useFormatter();
  // Arrow keys on a closed select change its value at once; a keyboard choice is saved on Enter or when leaving
  // the cell, so moving through the list does not save every teacher on the way.
  const [draft, setDraft] = useState<string | null>(null);
  const keyboard = useRef(false);
  const saved = cell.teacherId === null ? "" : String(cell.teacherId);
  const commit = (value: string) => {
    keyboard.current = false;
    setDraft(null);
    if (value !== saved) onAssign(cell, row, value ? Number(value) : null);
  };
  const onKeyDown = (event: KeyboardEvent<HTMLSelectElement>) => {
    if (["ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown"].includes(event.key)) keyboard.current = true;
    if (event.key === "Enter" && draft !== null) commit(draft);
  };
  const choices = teacherChoices(loads, line.subjectId, cell.teacherId, showAll);
  const teacher = loads.find((load) => load.teacherId === cell.teacherId);
  const options = [
    { value: "", label: text.noTeacher },
    ...choices.map((load) => ({ value: String(load.teacherId), label: text.teacherOption(load.shortName, format.number(load.assignedLessons), format.number(load.limit)) })),
  ];
  const label = text.cellLabel(line.label ? `${line.subjectName} - ${line.label}` : line.subjectName, row.label);
  return (
    <td className={`workload-cell${cell.teacherId === null ? " is-unassigned" : ""}`}>
      <Select aria-label={label} title={label} value={draft ?? saved} options={options} disabled={saving} data-row={rowIndex} data-col={colIndex}
        onKeyDown={onKeyDown}
        onBlur={() => { if (draft !== null) commit(draft); }}
        onChange={(event) => (keyboard.current ? setDraft(event.target.value) : commit(event.target.value))} />
      {cell.teacherId === null && <span className="workload-flag"><CircleDashed aria-hidden="true" size={16} />{text.unassigned}</span>}
      {cell.outsideSpecialization && teacher && (
        <span className="workload-flag is-warning">
          <TriangleAlert aria-hidden="true" size={16} />{text.outside}
          <Button size="sm" variant="secondary" icon={<Plus aria-hidden="true" size={16} />} onClick={() => onAddSpecialization(teacher, line)}>{text.addSpecialization}</Button>
        </span>
      )}
    </td>
  );
}

/** «حسب الشعبة» (Phase 3 §4): per stage, sections × curriculum lines, a teacher in each cell. */
export function WorkloadMatrix({ yearId, loads }: { yearId: number; loads: readonly TeacherLoad[] }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const [stageId, setStageId] = useState<number | null>(null);
  const [showAll, setShowAll] = useState(false);
  const matrix = useWorkloadMatrix(yearId, stageId);
  const save = useSetWorkloadCell(yearId);
  const addSpecialization = useAddSpecialization();
  const data = matrix.data;
  const stage = data?.stage ?? null;

  function assign(cell: WorkloadCell, row: WorkloadSectionRow, teacherId: number | null) {
    feedback.reset();
    save.mutate({ sectionId: row.sectionId, entryId: cell.entryId, teacherId, assignmentId: cell.assignmentId, version: cell.version }, {
      onSuccess: () => feedback.showSuccess(text.saved),
      onError: feedback.showError,
    });
  }

  function addToSpecializations(teacher: TeacherLoad, line: WorkloadLine) {
    feedback.reset();
    addSpecialization.mutate({ teacherId: teacher.teacherId, subjectId: line.subjectId, version: teacher.version }, {
      onSuccess: () => feedback.showSuccess(text.specializationAdded(teacher.fullName, line.subjectName)),
      onError: feedback.showError,
    });
  }

  if (matrix.isPending) return <Spinner label={messages.app.loadingContent} />;
  if (matrix.isError) return <Alert tone="error" message={messages.school.common.loadFailed} />;
  if (!data || data.stages.length === 0) return <EmptyState icon={<CircleDashed aria-hidden="true" size={24} />} message={text.noStages} />;

  const stageOptions = data.stages.map((item) => ({ value: String(item.stageId), label: text.stageOption(item.stageName, format.number(item.assignedCells), format.number(item.totalCells)) }));
  return (
    <div className="form-stack">
      <div className="list-toolbar">
        <Field id="workload-stage" label={text.stage}>
          <Select id="workload-stage" value={String(stage?.stageId ?? "")} options={stageOptions} onChange={(event) => { feedback.reset(); setStageId(Number(event.target.value)); }} />
        </Field>
        <span className="form-stack">
          <Checkbox checked={showAll} onChange={(event) => setShowAll(event.target.checked)}>{text.showAll}</Checkbox>
          <span className="card-note">{text.showAllHint}</span>
        </span>
      </div>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); void matrix.refetch(); }} loading={matrix.isFetching} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {stage && stage.lines.length === 0 && <Alert tone="info" message={text.noLines} />}
      {stage && stage.lines.length > 0 && (
        <EditGrid caption={stage.stageName} className="curriculum-table workload-table">
            <thead>
              <tr>
                <th scope="col">{text.section}</th>
                {stage.lines.map((line) => <th key={line.entryId} scope="col">{text.lineHeader(line.subjectName, line.label, format.number(line.weeklyLessons))}</th>)}
              </tr>
            </thead>
            <tbody>
              {stage.sections.map((row, rowIndex) => (
                <tr key={row.sectionId}>
                  <th scope="row">
                    <span className="workload-section">
                      <strong>{text.sectionHeader(row.label, row.shiftName)}</strong>
                      <span className={row.assignedLines === row.totalLines ? "workload-complete" : "card-note"}>
                        {text.completion(format.number(row.assignedLines), format.number(row.totalLines))}
                      </span>
                    </span>
                  </th>
                  {row.cells.map((cell, colIndex) => {
                    const line = stage.lines.find((item) => item.entryId === cell.entryId);
                    return line && (
                      <AssignmentCell key={cell.entryId} rowIndex={rowIndex} colIndex={colIndex} line={line} row={row} cell={cell} loads={loads} showAll={showAll}
                        saving={save.isPending} onAssign={assign} onAddSpecialization={addToSpecializations} />
                    );
                  })}
                </tr>
              ))}
            </tbody>
        </EditGrid>
      )}
    </div>
  );
}
