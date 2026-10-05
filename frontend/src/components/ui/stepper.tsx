import { Minus, Plus } from "lucide-react";
import type { KeyboardEvent } from "react";

type StepperProps = {
  id: string;
  /** Accessible name; the visible label is rendered by the caller (or a Field). */
  label: string;
  value: number;
  min: number;
  max: number;
  /** Formats the number with the school's numerals. */
  format: (value: number) => string;
  decreaseLabel: string;
  increaseLabel: string;
  disabled?: boolean;
  onChange: (value: number) => void;
};

/**
 * Number stepper (choose, don't type; spec 2.5 §5): − and + buttons around a spinbutton. Arrow keys,
 * Home and End change the value inside [min, max].
 */
export function Stepper({ id, label, value, min, max, format, decreaseLabel, increaseLabel, disabled, onChange }: StepperProps) {
  const set = (next: number) => onChange(Math.max(min, Math.min(max, next)));

  function onKeyDown(event: KeyboardEvent<HTMLSpanElement>) {
    const moves: Record<string, number> = { ArrowUp: value + 1, ArrowRight: value + 1, ArrowDown: value - 1, ArrowLeft: value - 1, Home: min, End: max };
    if (!(event.key in moves) || disabled) return;
    event.preventDefault();
    set(moves[event.key]);
  }

  return (
    <span className="ui-stepper">
      <button type="button" className="ui-stepper-button" aria-label={decreaseLabel} title={decreaseLabel} disabled={disabled || value <= min} onClick={() => set(value - 1)}>
        <Minus aria-hidden="true" size={16} />
      </button>
      <span
        id={id}
        role="spinbutton"
        tabIndex={disabled ? -1 : 0}
        className="ui-stepper-value"
        aria-label={label}
        aria-valuenow={value}
        aria-valuemin={min}
        aria-valuemax={max}
        aria-valuetext={format(value)}
        aria-disabled={disabled || undefined}
        onKeyDown={onKeyDown}
      >
        {format(value)}
      </span>
      <button type="button" className="ui-stepper-button" aria-label={increaseLabel} title={increaseLabel} disabled={disabled || value >= max} onClick={() => set(value + 1)}>
        <Plus aria-hidden="true" size={16} />
      </button>
    </span>
  );
}
