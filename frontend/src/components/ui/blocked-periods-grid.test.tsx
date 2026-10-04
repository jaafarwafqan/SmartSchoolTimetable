import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { BlockedPeriodsGrid, type GridSlot } from "./blocked-periods-grid";

function Harness({ initial = [] as GridSlot[] }) {
  const [value, setValue] = useState<GridSlot[]>(initial);
  return (
    <div dir="rtl">
      <BlockedPeriodsGrid
        label="grid"
        days={[{ day: 7, name: "الأحد" }, { day: 1, name: "الاثنين" }]}
        lessons={3}
        lessonLabel={(lesson) => `ح${lesson}`}
        cellLabel={(day, lesson, blocked) => `${day} ${lesson} ${blocked ? "محجوبة" : "متاحة"}`}
        value={value}
        onChange={setValue}
      />
      <output>{value.map((slot) => `${slot.day}-${slot.lessonNumber}`).join(",")}</output>
    </div>
  );
}

describe("BlockedPeriodsGrid", () => {
  it("is one tab stop, moves with arrow keys and toggles with click", () => {
    render(<Harness initial={[{ day: 1, lessonNumber: 3 }]} />);
    const first = screen.getByRole("button", { name: "الأحد 1 متاحة" });
    expect(first.tabIndex).toBe(0);
    expect(screen.getByRole("button", { name: "الاثنين 3 محجوبة" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getAllByRole("button").filter((button) => button.tabIndex === 0)).toHaveLength(1);

    first.focus();
    fireEvent.keyDown(first, { key: "ArrowDown" });
    const below = screen.getByRole("button", { name: "الاثنين 1 متاحة" });
    expect(below).toHaveFocus();
    fireEvent.keyDown(below, { key: "End" });
    const last = screen.getByRole("button", { name: "الاثنين 3 محجوبة" });
    expect(last).toHaveFocus();
    fireEvent.click(last);
    expect(screen.getByRole("status").textContent).toBe("");
    fireEvent.keyDown(screen.getByRole("button", { name: "الاثنين 3 متاحة" }), { key: "Home" });
    fireEvent.click(screen.getByRole("button", { name: "الاثنين 1 متاحة" }));
    expect(screen.getByRole("status")).toHaveTextContent("1-1");
  });

  it("renders nothing until days and lessons exist", () => {
    const { container } = render(
      <BlockedPeriodsGrid label="grid" days={[]} lessons={0} lessonLabel={String} cellLabel={() => ""} value={[]} onChange={() => undefined} />,
    );
    expect(container).toBeEmptyDOMElement();
  });
});
