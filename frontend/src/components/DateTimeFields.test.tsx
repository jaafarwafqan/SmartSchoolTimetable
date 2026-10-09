import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { ltrRuns } from "../i18n/isolate";
import { messages } from "../i18n/messages";
import { composeDate, DateField, splitDate } from "./DateField";
import { composeTime, minuteChoices, TimeField } from "./TimeField";
import { LtrText } from "./ui/ltr-text";
import { fromDigits, toDigits } from "./ui/segment-input";

// No school context is fetched in unit tests, so the formatter uses the defaults (Arabic-Indic digits).
function wrap(children: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { enabled: false, retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

const parts = messages.app.dateParts;
const time = messages.app.timeParts;

describe("DateField", () => {
  it("composes ISO dates and keeps incomplete input invalid for the server", () => {
    expect(composeDate({ day: 5, month: 9, year: 2026 })).toBe("2026-09-05");
    expect(composeDate({ day: null, month: null, year: null })).toBe("");
    expect(composeDate({ day: 5, month: null, year: 2026 })).toBe("2026--05");
    expect(splitDate("2027-01-31")).toEqual({ year: 2027, month: 1, day: 31 });
    expect(splitDate("31/01/2027")).toEqual({ day: null, month: null, year: null });
  });

  it("shows day/month/year with the school's digits and accepts typing in either digit set", () => {
    const onChange = vi.fn();
    render(wrap(<DateField id="start" label="البداية" defaultValue="2026-09-01" onChange={onChange} />));
    const day = screen.getByRole("textbox", { name: `البداية - ${parts.day}` });
    expect(day).toHaveValue("٠١");
    expect(screen.getByRole("textbox", { name: `البداية - ${parts.year}` })).toHaveValue("٢٠٢٦");
    fireEvent.change(day, { target: { value: "١٥" } });
    expect(onChange).toHaveBeenLastCalledWith("2026-09-15");
    fireEvent.keyDown(screen.getByRole("textbox", { name: `البداية - ${parts.month}` }), { key: "ArrowDown" });
    expect(onChange).toHaveBeenLastCalledWith("2026-08-15");
    expect(document.querySelector<HTMLInputElement>('input[name="start"]')?.value).toBe("2026-08-15");
  });
});

describe("TimeField", () => {
  it("is 12-hour with ص/م, minutes in 5-minute steps, and keeps 24-hour values for the API", () => {
    const onChange = vi.fn();
    render(wrap(<TimeField id="first" label="البدء" value="23:55" onChange={onChange} />));
    const hour = screen.getByRole("combobox", { name: `البدء - ${time.hours}` });
    const minute = screen.getByRole("combobox", { name: `البدء - ${time.minutes}` });
    const meridiem = screen.getByRole("combobox", { name: `البدء - ${time.meridiem}` });
    expect(hour).toHaveValue("11");
    expect(meridiem).toHaveValue("pm");
    fireEvent.change(meridiem, { target: { value: "am" } });
    expect(onChange).toHaveBeenLastCalledWith("11:55");
    fireEvent.change(hour, { target: { value: "12" } });
    expect(onChange).toHaveBeenLastCalledWith("00:55");
    fireEvent.change(minute, { target: { value: "5" } });
    expect(onChange).toHaveBeenLastCalledWith("00:05");
    expect(Array.from((minute as HTMLSelectElement).options).map((option) => option.value)).toEqual(["", ...minuteChoices(null).map(String)]);
    expect(minuteChoices(46)).toContain(46);
    expect(composeTime({ hour: 8, minute: null, meridiem: "am" })).toBe("");
    expect(screen.getByRole("group", { name: "البدء" })).toHaveAttribute("dir", "ltr");
    expect(screen.getByRole("group", { name: "البدء" }).textContent).not.toMatch(/\b(1[3-9]|2[0-3])\b/);
  });

  it("converts between digit sets", () => {
    expect(toDigits("08:45", "arab")).toBe("٠٨:٤٥");
    expect(toDigits("08:45", "latn")).toBe("08:45");
    expect(fromDigits("٠٨a4")).toBe("084");
  });
});

describe("LtrText and year order", () => {
  it("keeps a year range left-to-right inside RTL text", () => {
    const range = "2026 - 2027";
    render(<div dir="rtl"><LtrText>{range}</LtrText></div>);
    const year = screen.getByText(range);
    expect(year.tagName).toBe("BDI");
    expect(year).toHaveAttribute("dir", "ltr");
  });

  it("isolates numeric ranges and Latin runs in mixed Arabic strings", () => {
    expect(ltrRuns("السنة 2026 - 2027")).toBe("السنة ⁦2026 - 2027⁩");
    expect(ltrRuns("بدء 08:30 والمستخدم owner")).toBe("بدء ⁦08:30⁩ والمستخدم ⁦owner⁩");
    expect(ltrRuns("الأول المتوسط")).toBe("الأول المتوسط");
  });
});
