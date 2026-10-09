import { useState } from "react";
import type { FieldName } from "../i18n/messages";
import { messages } from "../i18n/messages";
import { formatNumber, numeralSystemOf } from "../lib/format";
import { useFormatter } from "../lib/schoolContext";
import { parseTime24, toTime24, type Meridiem } from "../lib/time";
import type { FieldErrors } from "../lib/useFormFeedback";
import { Field, fieldDescribedBy } from "./ui/field";
import { Select } from "./ui/select";

type Parts = { hour: number | null; minute: number | null; meridiem: Meridiem | null };

const emptyParts: Parts = { hour: null, minute: null, meridiem: null };

/** "HH:mm" (24-hour) when every part is chosen; "" otherwise (the server then reports REQUIRED). */
export function composeTime({ hour, minute, meridiem }: Parts): string {
  return hour === null || minute === null || meridiem === null ? "" : toTime24({ hour, minute, meridiem });
}

export function splitTime(value: string | null | undefined): Parts {
  return parseTime24(value) ?? emptyParts;
}

/** Minutes in 5-minute steps, plus the current minute when it is not on a step (times computed from 1-minute breaks). */
export function minuteChoices(current: number | null): number[] {
  const steps = Array.from({ length: 12 }, (_, index) => index * 5);
  return current !== null && !steps.includes(current) ? [...steps, current].sort((a, b) => a - b) : steps;
}

type TimeFieldProps = {
  id: string;
  label: string;
  name?: string;
  defaultValue?: string | null;
  value?: string;
  onChange?: (value: string) => void;
  required?: boolean;
  hint?: string;
  field?: FieldName;
  errors?: FieldErrors;
  /** Compact table rows: no visible label (the label stays the accessible name), error shown by the row. */
  compact?: boolean;
  invalid?: boolean;
};

/**
 * Design-system time field (R1, Iraqi 12-hour convention): hour 1–12, minute in 5-minute steps and ص/م, each a
 * native select (keyboard and screen readers work as usual, choose don't type). The value stays 24-hour "HH:mm"
 * for the API; no native time input, so the operating system's 24-hour display never appears.
 */
export function TimeField({ id, label, name, defaultValue, value, onChange, required, hint, field, errors, compact, invalid }: TimeFieldProps) {
  const format = useFormatter();
  const digits = numeralSystemOf(format.preferences.numeralSystem);
  const [parts, setParts] = useState<Parts>(() => splitTime(value ?? defaultValue));
  const [synced, setSynced] = useState(value);
  if (value !== undefined && value !== synced) {
    setSynced(value);
    setParts(splitTime(value));
  }
  const error = field ? errors?.[field] : undefined;
  const isInvalid = invalid || Boolean(error);
  const describedBy = compact ? undefined : fieldDescribedBy(id, hint, error);
  const text = messages.app.timeParts;
  const number = (value: number, pad = false) => formatNumber(value, digits).padStart(pad ? 2 : 0, digits === "arab" ? "٠" : "0");

  function update(next: Partial<Parts>) {
    const merged = { ...parts, ...next };
    setParts(merged);
    onChange?.(composeTime(merged));
  }

  const common = {
    "aria-invalid": isInvalid || undefined,
    "aria-describedby": describedBy,
    "data-field": field,
  };
  const placeholder = { value: "", label: text.empty };
  const segments = (
    <div className="ui-time-field" role="group" aria-label={label} dir="ltr">
      <Select id={id} className="ui-time-select" aria-label={`${label} - ${text.hours}`} value={parts.hour === null ? "" : String(parts.hour)}
        onChange={(event) => update({ hour: event.target.value === "" ? null : Number(event.target.value) })}
        options={[placeholder, ...Array.from({ length: 12 }, (_, index) => ({ value: String(index + 1), label: number(index + 1) }))]} {...common} />
      <span className="ui-segment-separator" aria-hidden="true">{text.separator}</span>
      <Select className="ui-time-select" aria-label={`${label} - ${text.minutes}`} value={parts.minute === null ? "" : String(parts.minute)}
        onChange={(event) => update({ minute: event.target.value === "" ? null : Number(event.target.value) })}
        options={[placeholder, ...minuteChoices(parts.minute).map((minute) => ({ value: String(minute), label: number(minute, true) }))]} {...common} />
      <Select className="ui-time-select ui-time-meridiem" aria-label={`${label} - ${text.meridiem}`} value={parts.meridiem ?? ""}
        onChange={(event) => update({ meridiem: event.target.value === "" ? null : event.target.value as Meridiem })}
        options={[placeholder, { value: "am", label: text.am }, { value: "pm", label: text.pm }]} {...common} />
      <input type="hidden" name={name ?? id} value={composeTime(parts)} />
    </div>
  );
  if (compact) return segments;
  return <Field id={id} label={label} required={required} hint={hint} error={error}>{segments}</Field>;
}
