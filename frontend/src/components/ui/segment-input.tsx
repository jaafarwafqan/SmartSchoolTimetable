import type { KeyboardEvent, Ref } from "react";

export type Digits = "arab" | "latn";

const arabicIndic = "٠١٢٣٤٥٦٧٨٩";

/** Shows a number with the school's digits (DESIGN_SYSTEM.md 8). */
export function toDigits(value: string, digits: Digits): string {
  return digits === "arab" ? value.replace(/[0-9]/g, (digit) => arabicIndic[Number(digit)]) : value;
}

/** Accepts both Arabic-Indic and Western digits and drops everything else. */
export function fromDigits(text: string): string {
  return text.replace(/[٠-٩]/g, (digit) => String(arabicIndic.indexOf(digit))).replace(/[^0-9]/g, "");
}

type SegmentInputProps = {
  id?: string;
  inputRef?: Ref<HTMLInputElement>;
  label: string;
  value: number | null;
  min: number;
  max: number;
  /** Displayed width; also the length after which focus moves to the next segment. */
  length: 2 | 4;
  step?: number;
  digits: Digits;
  invalid?: boolean;
  field?: string;
  describedBy?: string;
  onChange: (value: number | null) => void;
  /** Called when the segment is full, so the parent can move focus to the next one. */
  onFilled?: () => void;
};

/**
 * One numeric part of a date or time (day, month, year, hours, minutes). Typing accepts either digit set;
 * ArrowUp/ArrowDown change the value by `step` and wrap between `min` and `max`.
 */
export function SegmentInput({ id, inputRef, label, value, min, max, length, step = 1, digits, invalid, field, describedBy, onChange, onFilled }: SegmentInputProps) {
  const shown = value === null ? "" : toDigits(String(value).padStart(length, "0"), digits);

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key !== "ArrowUp" && event.key !== "ArrowDown") return;
    event.preventDefault();
    const span = max - min + 1;
    if (value === null) {
      onChange(event.key === "ArrowUp" ? min : max);
      return;
    }
    const delta = event.key === "ArrowUp" ? step : -step;
    onChange(((value - min + delta) % span + span) % span + min);
  }

  return (
    <input
      id={id}
      ref={inputRef}
      className={`ui-segment ui-segment-${length}`}
      type="text"
      inputMode="numeric"
      autoComplete="off"
      aria-label={label}
      aria-invalid={invalid ? true : undefined}
      aria-describedby={describedBy}
      data-field={field}
      value={shown}
      onKeyDown={onKeyDown}
      onChange={(event) => {
        const typed = fromDigits(event.target.value).slice(-length);
        if (typed === "") {
          onChange(null);
          return;
        }
        onChange(Number(typed));
        if (typed.length === length) onFilled?.();
      }}
    />
  );
}
