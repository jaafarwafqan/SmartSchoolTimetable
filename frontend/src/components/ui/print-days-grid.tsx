import type { ReactNode } from "react";
import { subjectColorClasses } from "./timetable-cell";
import type { PrintGridCell } from "./print-master-grid";

/** A section column: its label, and whether it opens a stage (a thick rule before it). */
export type PrintDaysColumn = { key: string; label: ReactNode; stageStart: boolean };
/** A stage over its sections. */
export type PrintDaysStage = { key: string; title: string; span: number; band: number };
/** One lesson row: the day label on the first row of the day (spanning all its lessons), the lesson number with its time, the cells. */
export type PrintDaysRow = { key: string; day?: { label: ReactNode; span: number }; number: ReactNode; cells: (PrintGridCell & { stageStart: boolean })[]; dayStart: boolean };

type PrintDaysGridProps = {
  caption: string;
  dayCorner: ReactNode;
  numberCorner: ReactNode;
  /** A header row with the stage names over their sections (left out on a page of one stage, whose title names it). */
  stages: PrintDaysStage[] | null;
  columns: PrintDaysColumn[];
  rows: PrintDaysRow[];
};

/**
 * The printed whole school BY DAYS (owner, 1.0.0 testing): rows are days × lessons (the day label spans its lessons, each row shows the
 * lesson number and its 12-hour time), columns are sections grouped by stage under a stage header row, with a thick rule between stages and
 * between days. Subject cells carry their colour through the palette class; black and white is handled by the print stylesheet.
 */
export function PrintDaysGrid({ caption, dayCorner, numberCorner, stages, columns, rows }: PrintDaysGridProps) {
  const headerRows = stages ? 2 : 1;
  return (
    <table className="print-days" data-columns={2 + columns.length}>
      <caption>{caption}</caption>
      <thead>
        {stages && (
          <tr className="print-days-stages">
            <th scope="col" rowSpan={headerRows} className="print-days-corner">{dayCorner}</th>
            <th scope="col" rowSpan={headerRows} className="print-days-corner print-days-number">{numberCorner}</th>
            {stages.map((stage, index) => (
              <th key={stage.key} scope="colgroup" colSpan={stage.span} className={`print-days-stage${index > 0 ? " is-stage-start" : ""}`} data-band={stage.band}>{stage.title}</th>
            ))}
          </tr>
        )}
        <tr>
          {!stages && <th scope="col" className="print-days-corner">{dayCorner}</th>}
          {!stages && <th scope="col" className="print-days-corner print-days-number">{numberCorner}</th>}
          {columns.map((column) => <th key={column.key} scope="col" className={`print-days-section${column.stageStart ? " is-stage-start" : ""}`}>{column.label}</th>)}
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.key} className={row.dayStart ? "is-day-start" : undefined}>
            {row.day && <th scope="rowgroup" rowSpan={row.day.span} className="print-days-day">{row.day.label}</th>}
            <th scope="row" className="print-days-number">{row.number}</th>
            {row.cells.map((cell) => (
              <td key={cell.key} className={[cell.subject && cell.color ? `print-cell ${subjectColorClasses[cell.color]}` : "print-cell", cell.stageStart ? "is-stage-start" : ""].filter(Boolean).join(" ")}>
                {cell.subject && <span className="ui-tt-subject">{cell.subject}</span>}
                {cell.subject && cell.detail && <span className="ui-tt-teacher">{cell.detail}</span>}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );
}
