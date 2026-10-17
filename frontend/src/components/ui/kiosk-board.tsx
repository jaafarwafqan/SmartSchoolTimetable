import type { ReactNode } from "react";

export type KioskBoardRow = { key: string | number; label: string; now: ReactNode; next: ReactNode };

type KioskBoardProps = {
  sectionHeader: string;
  nowHeader: ReactNode;
  nextHeader: ReactNode;
  rows: readonly KioskBoardRow[];
};

/** «شاشة العرض» board: one row per section with its current and next period (a real table, so screen readers announce the columns). */
export function KioskBoard({ sectionHeader, nowHeader, nextHeader, rows }: KioskBoardProps) {
  return (
    <table className="kiosk-table">
      <thead>
        <tr>
          <th scope="col">{sectionHeader}</th>
          <th scope="col">{nowHeader}</th>
          <th scope="col">{nextHeader}</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.key}>
            <th scope="row">{row.label}</th>
            <td>{row.now}</td>
            <td>{row.next}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
