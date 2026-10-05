import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EditGrid } from "./edit-grid";

const caption = "جدول";
const cellName = (row: number, col: number) => `خانة ${row} ${col}`;

function Grid() {
  return (
    <div dir="rtl">
      <EditGrid caption={caption}>
        <tbody>
          {[0, 1].map((row) => (
            <tr key={`row-${row}`}>
              {[0, 1].map((col) => (
                <td key={`cell-${row}-${col}`}><input aria-label={cellName(row, col)} data-row={row} data-col={col} /></td>
              ))}
            </tr>
          ))}
        </tbody>
      </EditGrid>
    </div>
  );
}

describe("EditGrid", () => {
  it("moves between cells with arrows; in RTL the left arrow goes to the next column", () => {
    render(<Grid />);
    const start = screen.getByRole("textbox", { name: cellName(0, 0) });
    start.focus();
    fireEvent.keyDown(start, { key: "ArrowLeft" });
    expect(screen.getByRole("textbox", { name: cellName(0, 1) })).toHaveFocus();
    fireEvent.keyDown(document.activeElement as HTMLElement, { key: "ArrowDown" });
    expect(screen.getByRole("textbox", { name: cellName(1, 1) })).toHaveFocus();
    fireEvent.keyDown(document.activeElement as HTMLElement, { key: "ArrowRight" });
    expect(screen.getByRole("textbox", { name: cellName(1, 0) })).toHaveFocus();
    fireEvent.keyDown(document.activeElement as HTMLElement, { key: "ArrowDown" }); // no row below: focus stays
    expect(screen.getByRole("textbox", { name: cellName(1, 0) })).toHaveFocus();
    fireEvent.keyDown(document.activeElement as HTMLElement, { key: "ArrowUp" });
    expect(start).toHaveFocus();
    expect(screen.getByRole("table", { name: caption })).toBeInTheDocument();
  });
});
