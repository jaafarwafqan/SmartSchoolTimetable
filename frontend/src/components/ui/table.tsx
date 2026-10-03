import type { ReactNode } from "react";
import { messages } from "../../i18n/messages";

export type TableColumn<Row> = {
  key: string;
  header: string;
  cell: (row: Row) => ReactNode;
  /** Numeric columns align to the end and use tabular numerals. */
  numeric?: boolean;
};

type DataTableProps<Row> = {
  caption: string;
  columns: readonly TableColumn<Row>[];
  rows: readonly Row[];
  rowKey: (row: Row) => string;
  selectedKey?: string | null;
  onSelect?: (row: Row) => void;
  loading?: boolean;
  /** Empty state: icon + sentence + primary action (DESIGN_SYSTEM.md 6.4). */
  empty?: ReactNode;
  /** Forces a visual row state for the style guide only. */
  demoHoverKey?: string;
};

const skeletonRows = 3;

/** DESIGN_SYSTEM.md 6.4: sticky header, 44px rows, hover and selected states, scrolls inside its container. */
export function DataTable<Row>({
  caption,
  columns,
  rows,
  rowKey,
  selectedKey,
  onSelect,
  loading = false,
  empty,
  demoHoverKey,
}: DataTableProps<Row>) {
  const showEmpty = !loading && rows.length === 0;
  return (
    <div className="ui-table-container">
      <table className="ui-table" aria-busy={loading || undefined}>
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.key} scope="col" className={column.numeric ? "is-numeric" : undefined}>
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {loading && Array.from({ length: skeletonRows }, (_, index) => (
            <tr key={`skeleton-${index}`} className="is-skeleton" aria-hidden="true">
              {columns.map((column) => <td key={column.key}><span className="ui-skeleton" /></td>)}
            </tr>
          ))}
          {!loading && rows.map((row) => {
            const key = rowKey(row);
            const selected = key === selectedKey;
            return (
              <tr
                key={key}
                className={[selected ? "is-selected" : "", key === demoHoverKey ? "is-hover" : ""].filter(Boolean).join(" ") || undefined}
                aria-selected={onSelect ? selected : undefined}
                tabIndex={onSelect ? 0 : undefined}
                onClick={onSelect ? () => onSelect(row) : undefined}
                onKeyDown={onSelect ? (event) => {
                  if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    onSelect(row);
                  }
                } : undefined}
              >
                {columns.map((column) => (
                  <td key={column.key} className={column.numeric ? "is-numeric" : undefined}>{column.cell(row)}</td>
                ))}
              </tr>
            );
          })}
          {showEmpty && (
            <tr>
              <td colSpan={columns.length} className="ui-table-empty">{empty}</td>
            </tr>
          )}
        </tbody>
      </table>
      {loading && <span className="sr-only" role="status">{messages.app.loadingContent}</span>}
    </div>
  );
}
