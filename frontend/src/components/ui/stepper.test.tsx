import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { BlockedPeriodsGrid, type GridSlot } from "./blocked-periods-grid";
import { Stepper } from "./stepper";

function StepperHarness() {
  const [value, setValue] = useState(6);
  return <Stepper id="thursday" label="حصص الخميس" value={value} min={0} max={7} format={String} decreaseLabel="إنقاص" increaseLabel="زيادة" onChange={setValue} />;
}

describe("Stepper", () => {
  it("changes by buttons and keys and stays inside its bounds", () => {
    render(<StepperHarness />);
    const value = screen.getByRole("spinbutton", { name: "حصص الخميس" });
    fireEvent.click(screen.getByRole("button", { name: "زيادة" }));
    expect(value).toHaveAttribute("aria-valuenow", "7");
    expect(screen.getByRole("button", { name: "زيادة" })).toBeDisabled();
    fireEvent.keyDown(value, { key: "Home" });
    expect(value).toHaveAttribute("aria-valuenow", "0");
    expect(screen.getByRole("button", { name: "إنقاص" })).toBeDisabled();
    fireEvent.keyDown(value, { key: "ArrowUp" });
    fireEvent.keyDown(value, { key: "End" });
    expect(value).toHaveAttribute("aria-valuenow", "7");
  });
});

function GridHarness() {
  const [value, setValue] = useState<GridSlot[]>([]);
  return (
    <>
      <BlockedPeriodsGrid label="grid" days={[{ day: 7, name: "الأحد" }, { day: 4, name: "الخميس" }]} lessons={3}
        lessonLabel={String} cellLabel={(day, lesson, blocked) => `${day} ${lesson} ${blocked ? "محجوبة" : "متاحة"}`}
        unavailableLabel={(day, lesson) => `${day} ${lesson} غير موجودة`}
        isAvailable={(slot) => !(slot.day === 4 && slot.lessonNumber === 3)} value={value} onChange={setValue} />
      <output>{value.length}</output>
    </>
  );
}

describe("BlockedPeriodsGrid per-day lessons", () => {
  it("marks lessons that do not exist on a day and never toggles them", () => {
    render(<GridHarness />);
    const missing = screen.getByRole("button", { name: "الخميس 3 غير موجودة" });
    expect(missing).toHaveAttribute("aria-disabled", "true");
    expect(missing).not.toHaveAttribute("aria-pressed");
    fireEvent.click(missing);
    expect(screen.getByRole("status")).toHaveTextContent("0");
    fireEvent.click(screen.getByRole("button", { name: "الخميس 2 متاحة" }));
    expect(screen.getByRole("status")).toHaveTextContent("1");
  });
});
