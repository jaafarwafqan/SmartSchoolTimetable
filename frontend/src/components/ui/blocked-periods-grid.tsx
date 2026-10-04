import { Ban } from "lucide-react";
import { useRef, useState, type KeyboardEvent } from "react";

export type GridSlot = { day: number; lessonNumber: number };

type BlockedPeriodsGridProps = {
  label: string;
  /** Working days in display order (ISO numbers) and their visible names. */
  days: readonly { day: number; name: string }[];
  lessons: number;
  /** Header text of each lesson column (already formatted with the school numerals). */
  lessonLabel: (lesson: number) => string;
  /** Accessible name of one cell, including its state. */
  cellLabel: (dayName: string, lesson: number, blocked: boolean) => string;
  value: readonly GridSlot[];
  onChange: (value: GridSlot[]) => void;
};

const isSame = (a: GridSlot, b: GridSlot) => a.day === b.day && a.lessonNumber === b.lessonNumber;

/**
 * Day × lesson grid of blocked slots (DESIGN_SYSTEM.md 6.5): blocked cells show a hatch pattern and a Ban icon.
 * Keyboard: one tab stop; arrow keys move between cells (mirrored for RTL), Home/End go to the row ends, and
 * Space or Enter toggles the focused cell.
 */
export function BlockedPeriodsGrid({ label, days, lessons, lessonLabel, cellLabel, value, onChange }: BlockedPeriodsGridProps) {
  const [focus, setFocus] = useState({ row: 0, column: 0 });
  const cells = useRef(new Map<string, HTMLButtonElement>());
  const lessonNumbers = Array.from({ length: lessons }, (_, index) => index + 1);

  function toggle(slot: GridSlot) {
    onChange(value.some((item) => isSame(item, slot)) ? value.filter((item) => !isSame(item, slot)) : [...value, slot]);
  }

  function move(row: number, column: number) {
    const next = { row: Math.max(0, Math.min(days.length - 1, row)), column: Math.max(0, Math.min(lessons - 1, column)) };
    setFocus(next);
    cells.current.get(`${next.row}:${next.column}`)?.focus();
  }

  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>, row: number, column: number) {
    const rtl = getComputedStyle(event.currentTarget).direction === "rtl";
    const forward = rtl ? "ArrowLeft" : "ArrowRight";
    const backward = rtl ? "ArrowRight" : "ArrowLeft";
    const moves: Record<string, [number, number]> = {
      [forward]: [row, column + 1],
      [backward]: [row, column - 1],
      ArrowDown: [row + 1, column],
      ArrowUp: [row - 1, column],
      Home: [row, 0],
      End: [row, lessons - 1],
    };
    const target = moves[event.key];
    if (!target) return;
    event.preventDefault();
    move(target[0], target[1]);
  }

  if (days.length === 0 || lessons === 0) return null;
  return (
    <div className="ui-blocked-grid-scroll">
      <table className="ui-blocked-grid" aria-label={label}>
        <thead>
          <tr>
            <td />
            {lessonNumbers.map((lesson) => <th key={lesson} scope="col">{lessonLabel(lesson)}</th>)}
          </tr>
        </thead>
        <tbody>
          {days.map(({ day, name }, row) => (
            <tr key={day}>
              <th scope="row">{name}</th>
              {lessonNumbers.map((lesson, column) => {
                const slot = { day, lessonNumber: lesson };
                const blocked = value.some((item) => isSame(item, slot));
                return (
                  <td key={lesson}>
                    <button
                      type="button"
                      ref={(element) => { if (element) cells.current.set(`${row}:${column}`, element); else cells.current.delete(`${row}:${column}`); }}
                      className={`ui-blocked-cell${blocked ? " is-blocked" : ""}`}
                      aria-pressed={blocked}
                      aria-label={cellLabel(name, lesson, blocked)}
                      title={cellLabel(name, lesson, blocked)}
                      tabIndex={focus.row === row && focus.column === column ? 0 : -1}
                      onFocus={() => setFocus({ row, column })}
                      onKeyDown={(event) => onKeyDown(event, row, column)}
                      onClick={() => toggle(slot)}
                    >
                      {blocked && <Ban aria-hidden="true" size={16} />}
                    </button>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
