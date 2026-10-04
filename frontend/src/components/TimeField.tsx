import { useRef, useState } from "react";
import type { FieldName } from "../i18n/messages";
import { messages } from "../i18n/messages";
import { numeralSystemOf } from "../lib/format";
import { useFormatter } from "../lib/schoolContext";
import type { FieldErrors } from "../lib/useFormFeedback";
import { Field, fieldDescribedBy } from "./ui/field";
import { SegmentInput } from "./ui/segment-input";

type Parts = { hours: number | null; minutes: number | null };

/** "HH:mm" (24-hour) when complete; "" when empty; otherwise an incomplete value the server reports as INVALID_TIME. */
export function composeTime({ hours, minutes }: Parts): string {
  if (hours === null && minutes === null) return "";
  const pad = (value: number | null) => (value === null ? "" : String(value).padStart(2, "0"));
  return `${pad(hours)}:${pad(minutes)}`;
}

export function splitTime(value: string | null | undefined): Parts {
  const match = /^(\d{2}):(\d{2})$/.exec(value ?? "");
  return match ? { hours: Number(match[1]), minutes: Number(match[2]) } : { hours: null, minutes: null };
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
 * Design-system 24-hour time field (spec 2.5 §2.2): hours and minutes in HH:mm order (kept left-to-right),
 * the school's numerals, typing and arrow keys (minutes step 5). No native time input, so no AM/PM.
 */
export function TimeField({ id, label, name, defaultValue, value, onChange, required, hint, field, errors, compact, invalid }: TimeFieldProps) {
  const format = useFormatter();
  const digits = numeralSystemOf(format.preferences.numeralSystem);
  const [parts, setParts] = useState<Parts>(() => splitTime(value ?? defaultValue));
  const [synced, setSynced] = useState(value);
  const minutes = useRef<HTMLInputElement | null>(null);
  if (value !== undefined && value !== synced) {
    setSynced(value);
    setParts(splitTime(value));
  }
  const error = field ? errors?.[field] : undefined;
  const isInvalid = invalid || Boolean(error);
  const describedBy = compact ? undefined : fieldDescribedBy(id, hint, error);
  const text = messages.app.timeParts;

  function update(next: Partial<Parts>) {
    const merged = { ...parts, ...next };
    setParts(merged);
    onChange?.(composeTime(merged));
  }

  const segments = (
    <div className="ui-segmented ui-segmented-ltr" role="group" aria-label={label} dir="ltr">
      <SegmentInput id={id} label={`${label} - ${text.hours}`} value={parts.hours} min={0} max={23} length={2} digits={digits}
        invalid={isInvalid} field={field} describedBy={describedBy} onChange={(hours) => update({ hours })}
        onFilled={() => minutes.current?.focus()} />
      <span className="ui-segment-separator" aria-hidden="true">{text.separator}</span>
      <SegmentInput inputRef={minutes} label={`${label} - ${text.minutes}`} value={parts.minutes} min={0} max={59} length={2} step={5} digits={digits}
        invalid={isInvalid} field={field} describedBy={describedBy} onChange={(value) => update({ minutes: value })} />
      <input type="hidden" name={name ?? id} value={composeTime(parts)} />
    </div>
  );
  if (compact) return segments;
  return <Field id={id} label={label} required={required} hint={hint} error={error}>{segments}</Field>;
}
