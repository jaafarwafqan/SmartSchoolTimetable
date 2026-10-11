import type { ReactNode } from "react";
import { subjectColorClasses, type SubjectColorIndex } from "./timetable-cell";

export type PrintGridCell = { key: string; subject?: string; detail?: string; color?: SubjectColorIndex };
export type PrintGridRow = { key: string; label: ReactNode; cells: PrintGridCell[] };
export type PrintGridGroup = { key: string; title: string; rows: PrintGridRow[] };

type PrintMasterGridProps = {
  caption: string;
  corner: ReactNode;
  /** One header per day, spanning `span` lesson columns. */
  dayHeaders: { key: string; label: ReactNode; span: number }[];
  /** One header per lesson column, in the same order as the cells of a row. */
  numberHeaders: { key: string; label: ReactNode }[];
  groups: PrintGridGroup[];
  /** A full-width header row per group (stage), a thick rule between groups and a tinted first column. */
  groupRows: boolean;
};

/**
 * The printed whole-school grid: rows down, days × lessons across, split into groups (stages). Each group has a header row with the
 * stage's name (so it reads in black and white), a thick rule before the next group, and a light tint on its first column in colour
 * mode. Subject cells carry their colour through the palette utility class; the print stylesheet turns it off for black and white.
 * Nothing here is interactive: it exists only inside the print document.
 */
export function PrintMasterGrid({ caption, corner, dayHeaders, numberHeaders, groups, groupRows }: PrintMasterGridProps) {
  const columns = 1 + numberHeaders.length;
  return (
    <table className="print-master" data-columns={columns}>
      <caption>{caption}</caption>
      <thead>
        <tr>
          <th scope="col" rowSpan={2} className="print-corner">{corner}</th>
          {dayHeaders.map((header) => <th key={header.key} scope="colgroup" colSpan={header.span}>{header.label}</th>)}
        </tr>
        <tr>
          {numberHeaders.map((header) => <th key={header.key} scope="col" className="print-number">{header.label}</th>)}
        </tr>
      </thead>
      {groups.map((group, index) => (
        <tbody key={group.key} className="print-stage" data-band={(index % 6) + 1}>
          {groupRows && (
            <tr className="print-stage-row">
              <th scope="colgroup" colSpan={columns}>{group.title}</th>
            </tr>
          )}
          {group.rows.map((row) => (
            <tr key={row.key}>
              <th scope="row" className="print-section">{row.label}</th>
              {row.cells.map((cell) => (
                <td key={cell.key} className={cell.subject && cell.color ? `print-cell ${subjectColorClasses[cell.color]}` : "print-cell"}>
                  {cell.subject && <span className="ui-tt-subject">{cell.subject}</span>}
                  {cell.subject && cell.detail && <span className="ui-tt-teacher">{cell.detail}</span>}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      ))}
    </table>
  );
}
