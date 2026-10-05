import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { LoadBar, loadPercent } from "../../components/LoadBar";
import { messages } from "../../i18n/messages";
import { arabicCount } from "../../lib/arabicCount";
import { createFormatter, formatNumber } from "../../lib/format";
import { PlanView } from "./BulkActions";
import { overloaded, teacherChoices, type TeacherLoad, type WorkloadPlan } from "./workloadApi";

const text = messages.school.workload;
const format = createFormatter();

function load(id: number, specializations: number[], assigned: number, limit: number, status: TeacherLoad["status"]): TeacherLoad {
  return { teacherId: id, fullName: `معلم ${id}`, shortName: `م${id}`, specializationIds: specializations, assignedLessons: assigned, maxPerWeek: limit,
    available: 30, limit, status, released: false, assignments: [], version: 1 };
}

const loads = [load(1, [10], 10, 12, "within"), load(2, [20], 28, 25, "over"), load(3, [10, 20], 31, 24, "over")];

describe("teacher chooser", () => {
  it("offers the subject's specialists, the current teacher, or everyone", () => {
    expect(teacherChoices(loads, 10, null, false).map((item) => item.teacherId)).toEqual([1, 3]);
    expect(teacherChoices(loads, 10, 2, false).map((item) => item.teacherId)).toEqual([1, 2, 3]);
    expect(teacherChoices(loads, 99, null, true)).toHaveLength(3);
  });
});

describe("quick shortage warnings", () => {
  it("lists overloaded teachers, the largest excess first, with the exact numbers", () => {
    const over = overloaded(loads);
    expect(over.map((item) => item.teacherId)).toEqual([3, 2]);
    expect(text.shortage(over[1].fullName, format.number(28), format.number(25), format.number(3))).toContain("المسند ٢٨، المتاح ٢٥، يزيد ٣");
  });
});

describe("load bar", () => {
  it("fills to the share of the limit and caps an overload", () => {
    expect([loadPercent(6, 12), loadPercent(28, 25), loadPercent(0, 0), loadPercent(2, 0)]).toEqual([50, 100, 0, 100]);
    render(<LoadBar label="نصاب" assigned={6} limit={12} status="within" />);
    const meter = screen.getByRole("meter", { name: "نصاب" });
    expect(meter.getAttribute("aria-valuenow")).toBe("6");
    expect(meter.firstElementChild?.getAttribute("style")).toContain("inline-size: 50%");
  });
});

describe("bulk plan preview", () => {
  it("names each line's action and shows loads before and after", () => {
    const plan: WorkloadPlan = {
      changes: 1,
      lines: [
        { sectionId: 1, stageName: "الأول", sectionLabel: "أ", entryId: 5, subjectName: "الرياضيات", label: null, weeklyLessons: 5, currentTeacher: null, newTeacher: "أحمد", action: "create" },
        { sectionId: 2, stageName: "الأول", sectionLabel: "ب", entryId: 5, subjectName: "الرياضيات", label: null, weeklyLessons: 5, currentTeacher: "علي", newTeacher: "علي", action: "skip" },
      ],
      loads: [{ teacherId: 1, fullName: "أحمد", before: 0, after: 5, limit: 24 }],
    };
    render(<PlanView plan={plan} format={format} />);
    expect(screen.getByText(text.bulk.actions.create)).toBeTruthy();
    expect(screen.getByText(text.bulk.actions.skip)).toBeTruthy();
    expect(screen.getByText(/٠ ← ٥/)).toBeTruthy();
  });

  it("counts assignments with Arabic agreement", () => {
    const arab = (value: number) => formatNumber(value, "arab");
    expect([1, 2, 3, 11].map((value) => arabicCount(value, "assignment", arab))).toEqual(["نصاب واحد", "نصابان", "٣ أنصبة", "١١ نصاباً"]);
  });
});
