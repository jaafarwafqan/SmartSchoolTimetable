import type { KeyboardEvent, ReactNode } from "react";

type EditGridProps = {
  /** Visually hidden caption: the table's accessible name. */
  caption: string;
  className?: string;
  children: ReactNode;
};

/**
 * Table of inputs edited cell by cell (spec 2.5 §3.3). Each input carries data-row and data-col; arrow keys and
 * Enter move between them. Right/left follow the reading direction, so in RTL ArrowLeft moves to the next column.
 */
export function EditGrid({ caption, className = "", children }: EditGridProps) {
  function onKeyDown(event: KeyboardEvent<HTMLTableElement>) {
    const target = event.target as HTMLElement;
    if (target.dataset.row === undefined || target.dataset.col === undefined) return;
    const row = Number(target.dataset.row);
    const col = Number(target.dataset.col);
    const rtl = (target.closest("[dir]")?.getAttribute("dir") ?? "rtl") === "rtl";
    const moves: Record<string, [number, number]> = {
      ArrowUp: [row - 1, col],
      ArrowDown: [row + 1, col],
      Enter: [row + 1, col],
      [rtl ? "ArrowLeft" : "ArrowRight"]: [row, col + 1],
      [rtl ? "ArrowRight" : "ArrowLeft"]: [row, col - 1],
    };
    const next = moves[event.key];
    if (!next) return;
    const cell = event.currentTarget.querySelector<HTMLInputElement>(`input[data-row="${next[0]}"][data-col="${next[1]}"]`);
    if (!cell) return;
    event.preventDefault();
    cell.focus();
    cell.select();
  }

  return (
    <div className="ui-table-container">
      <table className={`ui-table ui-edit-grid ${className}`.trim()} onKeyDown={onKeyDown}>
        <caption className="sr-only">{caption}</caption>
        {children}
      </table>
    </div>
  );
}
