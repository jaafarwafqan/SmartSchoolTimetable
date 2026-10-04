import { useRef, useState } from "react";
import { messages } from "../i18n/messages";
import type { FieldName } from "../i18n/messages";
import { numeralSystemOf } from "../lib/format";
import { useFormatter } from "../lib/schoolContext";
import type { FieldErrors } from "../lib/useFormFeedback";
import { Field, fieldDescribedBy } from "./ui/field";
import { SegmentInput } from "./ui/segment-input";

type Parts = { day: number | null; month: number | null; year: number | null };

/** "yyyy-MM-dd" when complete; "" when empty; otherwise an incomplete string the server reports as INVALID_DATE. */
export function composeDate({ day, month, year }: Parts): string {
  if (day === null && month === null && year === null) return "";
  const pad = (value: number | null, length: number) => (value === null ? "" : String(value).padStart(length, "0"));
  return `${pad(year, 4)}-${pad(month, 2)}-${pad(day, 2)}`;
}

export function splitDate(value: string | null | undefined): Parts {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value ?? "");
  return match ? { year: Number(match[1]), month: Number(match[2]), day: Number(match[3]) } : { day: null, month: null, year: null };
}

type DateFieldProps = {
  id: string;
  label: string;
  /** Form field name of the hidden ISO value (defaults to `id`). */
  name?: string;
  defaultValue?: string | null;
  value?: string;
  onChange?: (value: string) => void;
  required?: boolean;
  hint?: string;
  field?: FieldName;
  errors?: FieldErrors;
};

/**
 * Design-system date field (spec 2.5 §2.2): day / month / year segments in that order, the school's numerals,
 * typing and arrow keys, the full Arabic date as a hint, and an ISO "yyyy-MM-dd" value for the API.
 * No native date input, so no browser-locale formats.
 */
export function DateField({ id, label, name, defaultValue, value, onChange, required, hint, field, errors }: DateFieldProps) {
  const format = useFormatter();
  const digits = numeralSystemOf(format.preferences.numeralSystem);
  const [parts, setParts] = useState<Parts>(() => splitDate(value ?? defaultValue));
  const [synced, setSynced] = useState(value);
  const month = useRef<HTMLInputElement | null>(null);
  const year = useRef<HTMLInputElement | null>(null);
  if (value !== undefined && value !== synced) {
    setSynced(value);
    setParts(splitDate(value));
  }
  const iso = composeDate(parts);
  const error = field ? errors?.[field] : undefined;
  const preview = /^\d{4}-\d{2}-\d{2}$/.test(iso) && !Number.isNaN(Date.parse(`${iso}T00:00:00Z`)) ? format.date(iso) : undefined;
  const describedBy = fieldDescribedBy(id, hint ?? preview, error);
  const text = messages.app.dateParts;

  function update(next: Partial<Parts>) {
    const merged = { ...parts, ...next };
    setParts(merged);
    onChange?.(composeDate(merged));
  }

  return (
    <Field id={id} label={label} required={required} hint={hint ?? preview} error={error}>
      <div className="ui-segmented" role="group" aria-label={label}>
        <SegmentInput id={id} label={`${label} - ${text.day}`} value={parts.day} min={1} max={31} length={2} digits={digits}
          invalid={Boolean(error)} field={field} describedBy={describedBy} onChange={(day) => update({ day })} onFilled={() => month.current?.focus()} />
        <span className="ui-segment-separator" aria-hidden="true">{text.separator}</span>
        <SegmentInput inputRef={month} label={`${label} - ${text.month}`} value={parts.month} min={1} max={12} length={2} digits={digits}
            invalid={Boolean(error)} field={field} describedBy={describedBy} onChange={(value) => update({ month: value })} onFilled={() => year.current?.focus()} />
        <span className="ui-segment-separator" aria-hidden="true">{text.separator}</span>
        <SegmentInput inputRef={year} label={`${label} - ${text.year}`} value={parts.year} min={2000} max={2100} length={4} digits={digits}
            invalid={Boolean(error)} field={field} describedBy={describedBy} onChange={(value) => update({ year: value })} />
      </div>
      <input type="hidden" name={name ?? id} value={iso} />
    </Field>
  );
}
