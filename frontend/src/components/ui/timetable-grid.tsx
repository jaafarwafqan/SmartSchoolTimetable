import type { KeyboardEvent, ReactNode } from "react";
import { TimetableCell, type SubjectColorIndex, type TimetableCellState } from "./timetable-cell";

/** `boundary`: the column opens a group (a stage in the school view by days) and gets a thick rule before it. */
export type GridColumnHeader = { key: string; label: ReactNode; span?: number; boundary?: boolean };

export type GridCell = {
  key: string;
  subject?: string;
  /** The caption line: the teacher (by section) or the section (by teacher). */
  detail?: string;
  color?: SubjectColorIndex;
  state?: TimetableCellState;
  /** Screen-reader and tooltip text of the cell. */
  description: string;
  /** Optional action on Enter or click (the manual editor); read-only grids leave it out. */
  onActivate?: () => void;
  /** The cell opens a column group (a thick rule before it). */
  boundary?: boolean;
};

/**
 * `group`: on the first row of a group (a day in the school view by days), a row header spanning `span` rows before the row's own label;
 * `groupStart` gives the row a thick rule above it.
 */
export type GridRow = { key: string; label: ReactNode; cells: GridCell[]; group?: { label: ReactNode; span: number }; groupStart?: boolean };

type TimetableGridProps = {
  caption: string;
  /** Header of the row-label column. */
  corner: ReactNode;
  /** One or more header rows; the last one has one entry per cell column. */
  headerRows: GridColumnHeader[][];
  /**
   * Optional row headers for the header rows after the first (for example the clock row of each daily session). When
   * given, the corner covers the first header row only.
   */
  headerRowLabels?: ReactNode[];
  rows: GridRow[];
  className?: string;
  /** Header of the row-group column, when rows have groups (for example «اليوم» over the day labels). */
  groupCorner?: ReactNode;
};

/**
 * DESIGN_SYSTEM.md 6.5 timetable grid: a real table (row and column headers for screen readers) whose cells are
 * TimetableCell. Arrow keys move focus between cells (in RTL the left arrow goes to the next column); Home and
 * End jump within the row; Enter or Space activates a cell that has an action. The grid scrolls inside its own
 * container only (never the page).
 */
export function TimetableGrid({ caption, corner, headerRows, headerRowLabels, rows, className = "", groupCorner }: TimetableGridProps) {
  const grouped = groupCorner !== undefined;
  function onKeyDown(event: KeyboardEvent<HTMLTableElement>) {
    const cell = (event.target as HTMLElement).closest<HTMLElement>("[data-cell]");
    if (!cell) return;
    const [row, column] = (cell.dataset.cell ?? "0:0").split(":").map(Number);
    const table = event.currentTarget;
    const focus = (selector: string) => {
      const next = table.querySelector<HTMLElement>(selector);
      if (!next) return;
      event.preventDefault();
      next.focus();
    };
    switch (event.key) {
      case "ArrowUp": return focus(`[data-cell="${row - 1}:${column}"] .ui-tt-cell`);
      case "ArrowDown": return focus(`[data-cell="${row + 1}:${column}"] .ui-tt-cell`);
      case "ArrowLeft": return focus(`[data-cell="${row}:${column + 1}"] .ui-tt-cell`);
      case "ArrowRight": return focus(`[data-cell="${row}:${column - 1}"] .ui-tt-cell`);
      case "Home": return focus(`[data-cell="${row}:0"] .ui-tt-cell`);
      case "End": {
        const cells = table.querySelectorAll<HTMLElement>(`[data-cell^="${row}:"] .ui-tt-cell`);
        if (cells.length === 0) return;
        event.preventDefault();
        cells.item(cells.length - 1).focus();
        return;
      }
      case "Enter":
      case " ": {
        const target = rows[row]?.cells[column];
        if (!target?.onActivate) return;
        event.preventDefault();
        target.onActivate();
        return;
      }
      default:
    }
  }

  return (
    <div className="ui-table-container ui-tt-grid-container">
      <table className={`ui-tt-grid ${className}`.trim()} onKeyDown={onKeyDown}>
        <caption>{caption}</caption>
        <thead>
          {headerRows.map((headers, index) => (
            <tr key={index}>
              {index === 0 && grouped && <th scope="col" rowSpan={headerRows.length} className="ui-tt-group-corner">{groupCorner}</th>}
              {index === 0 && <th scope="col" rowSpan={headerRowLabels ? 1 : headerRows.length}>{corner}</th>}
              {index > 0 && headerRowLabels && <th scope="row" className="ui-tt-header-label">{headerRowLabels[index]}</th>}
              {headers.map((header) => (
                <th key={header.key} scope={header.span && header.span > 1 ? "colgroup" : "col"} colSpan={header.span} className={header.boundary ? "is-group-start" : undefined}>{header.label}</th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => (
            <tr key={row.key} className={row.groupStart ? "is-group-start" : undefined}>
              {row.group && <th scope="rowgroup" rowSpan={row.group.span} className="ui-tt-group">{row.group.label}</th>}
              <th scope="row">{row.label}</th>
              {row.cells.map((cell, columnIndex) => (
                <td key={cell.key} data-cell={`${rowIndex}:${columnIndex}`} onClick={cell.onActivate} className={cell.boundary ? "is-group-start" : undefined}>
                  <TimetableCell subject={cell.subject} teacher={cell.detail} color={cell.color} state={cell.state} description={cell.description} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
