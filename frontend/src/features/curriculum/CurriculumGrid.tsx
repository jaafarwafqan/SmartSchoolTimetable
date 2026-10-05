import { CircleAlert, CircleCheck, CircleX } from "lucide-react";
import { useRef, useState } from "react";
import { Badge } from "../../components/ui/badge";
import { EditGrid } from "../../components/ui/edit-grid";
import { Input } from "../../components/ui/input";
import { fromDigits } from "../../components/ui/segment-input";
import { subjectColorClasses, type SubjectColorIndex } from "../../components/ui/timetable-cell";
import { messages } from "../../i18n/messages";
import type { CellInput, CurriculumCell, CurriculumRow, CurriculumTable, ShiftTotal } from "./curriculumApi";

const text = messages.school.curriculum;
export const minLessons = 1;
export const maxLessons = 15;

type GridProps = {
  table: CurriculumTable;
  format: (value: number) => string;
  saving: boolean;
  onSave: (input: CellInput, onDone: (ok: boolean) => void) => void;
};

/** Parses a typed cell: empty clears it, otherwise a whole number in range (Arabic-Indic digits accepted). */
export function parseLessons(raw: string): { ok: true; value: number | null } | { ok: false } {
  const trimmed = fromDigits(raw).trim();
  if (trimmed === "") return { ok: true, value: null };
  if (!/^\d+$/.test(trimmed)) return { ok: false };
  const value = Number(trimmed);
  return value >= minLessons && value <= maxLessons ? { ok: true, value } : { ok: false };
}

function statusText(total: ShiftTotal, format: (value: number) => string) {
  if (total.status === "equal") return text.status.equal;
  return total.status === "under" ? text.status.under(format(total.difference)) : text.status.over(format(-total.difference));
}

/** Status chip (icon + text); in a two-shift stage the text names the shift. */
function TotalBadge({ total, format, withShift }: { total: ShiftTotal; format: (value: number) => string; withShift: boolean }) {
  const label = withShift ? text.shiftStatus(total.shiftName, statusText(total, format)) : statusText(total, format);
  if (total.status === "equal") return <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={14} />}>{label}</Badge>;
  if (total.status === "under") return <Badge tone="warning" icon={<CircleAlert aria-hidden="true" size={14} />}>{label}</Badge>;
  return <Badge tone="danger" icon={<CircleX aria-hidden="true" size={14} />}>{label}</Badge>;
}

const capacities = (totals: readonly ShiftTotal[], format: (value: number) => string) =>
  [...new Set(totals.map((total) => total.weeklyCapacity))].map(format).join(" / ");

/** One editable cell: local draft, saved on Enter or when focus leaves; Escape restores the saved value. */
function LessonCell({ row, cell, stageName, rowIndex, colIndex, format, disabled, onSave }: {
  row: CurriculumRow;
  cell: CurriculumCell;
  stageName: string;
  rowIndex: number;
  colIndex: number;
  format: (value: number) => string;
  disabled: boolean;
  onSave: GridProps["onSave"];
}) {
  const saved = cell.weeklyLessons === null ? "" : format(cell.weeklyLessons);
  const [draft, setDraft] = useState<string | null>(null);
  const [invalid, setInvalid] = useState(false);
  /** Enter saves and moves focus, and the blur would save again: one save at a time per cell. */
  const pending = useRef(false);
  const shown = draft ?? saved;

  function commit() {
    if (draft === null || draft === saved) { setDraft(null); return; }
    const parsed = parseLessons(draft);
    if (!parsed.ok) { setInvalid(true); return; }
    if (parsed.value === cell.weeklyLessons || (parsed.value === null && cell.entryId === null)) { setDraft(null); setInvalid(false); return; }
    if (pending.current) return;
    pending.current = true;
    onSave(
      { stageId: cell.stageId, subjectId: row.subjectId, label: row.label, weeklyLessons: parsed.value, entryId: cell.entryId, version: cell.version },
      (ok) => { pending.current = false; if (ok) { setDraft(null); setInvalid(false); } },
    );
  }

  return (
    <td className={`curriculum-cell${invalid ? " is-invalid" : ""}`}>
      <Input
        className="curriculum-input"
        inputMode="numeric"
        autoComplete="off"
        value={shown}
        disabled={disabled}
        data-row={rowIndex}
        data-col={colIndex}
        aria-label={text.cell(row.label ? `${row.subjectName} - ${row.label}` : row.subjectName, stageName)}
        aria-invalid={invalid || undefined}
        aria-describedby="curriculum-cell-hint"
        onChange={(event) => { setDraft(event.target.value); setInvalid(false); }}
        onBlur={commit}
        onKeyDown={(event) => {
          if (event.key === "Enter") commit();
          if (event.key === "Escape") { setDraft(null); setInvalid(false); }
        }}
      />
      {cell.duplicates > 0 && (
        <span className="curriculum-duplicates" title={text.duplicatesTitle}>{text.duplicates(format(cell.duplicates))}</span>
      )}
    </td>
  );
}

/**
 * The curriculum table (spec 2.5 §3.3): subjects (and their repeated lines) down, stages across. Arrow keys move
 * between cells (EditGrid); totals per stage and shift sit below.
 */
export function CurriculumGrid({ table, format, saving, onSave }: GridProps) {
  return (
    <div className="curriculum-scroll">
      <EditGrid caption={text.tableCaption} className="curriculum-table">
        <thead>
          <tr>
            <th scope="col" className="curriculum-subject-head">{text.subject}</th>
            {table.stages.map((stage) => (
              <th key={`head-${stage.id}`} scope="col" className="curriculum-stage-head">
                <span className="curriculum-stage-name">{stage.name}</span>
                {stage.totals.length > 0 && <span className="curriculum-stage-capacity">{text.headerCapacity(capacities(stage.totals, format))}</span>}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {table.rows.map((row, rowIndex) => (
            <tr key={`row-${row.subjectId}-${row.label ?? ""}`} className={row.label ? "curriculum-repeat-row" : undefined}>
              <th scope="row" className="curriculum-subject">
                <span className={`ui-subject-dot ${subjectColorClasses[row.colorIndex as SubjectColorIndex] ?? ""}`} aria-hidden="true" />
                <span>{row.subjectName}</span>
                {row.label && <span className="curriculum-label">{row.label}</span>}
              </th>
              {row.cells.map((cell, colIndex) => (
                <LessonCell key={`cell-${row.subjectId}-${row.label ?? ""}-${cell.stageId}`} row={row} cell={cell} rowIndex={rowIndex} colIndex={colIndex}
                  stageName={table.stages[colIndex]?.name ?? ""} format={format} disabled={saving && cell.entryId === null} onSave={onSave} />
              ))}
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <th scope="row">{text.totalsRow}</th>
            {table.stages.map((stage) => (
              <td key={`total-${stage.id}`} className="curriculum-total">
                <div className="curriculum-total-box">
                  <strong className="curriculum-total-sum">
                    {stage.totals.length === 0 ? format(stage.plannedLessons) : text.plannedOf(format(stage.plannedLessons), capacities(stage.totals, format))}
                  </strong>
                  {stage.totals.length === 0
                    ? <span className="curriculum-total-note">{text.noSections}</span>
                    : (
                      <span className="curriculum-total-chips">
                        {stage.totals.map((total) => <TotalBadge key={`total-${stage.id}-${total.shiftId}`} total={total} format={format} withShift={stage.totals.length > 1} />)}
                      </span>
                    )}
                </div>
              </td>
            ))}
          </tr>
        </tfoot>
      </EditGrid>
    </div>
  );
}
