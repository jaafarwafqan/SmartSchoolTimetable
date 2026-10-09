import { Check } from "lucide-react";

type ChipGroupProps = {
  /** Accessible name of the group. */
  label: string;
  /** Optional short visible caption before the chips (for example the unit when the chips show bare numbers). */
  caption?: string;
  /** `label` is visible; `name` (optional) is the full accessible name, for example «٥ دقائق» for the chip «٥». */
  options: readonly { value: number; label: string; name?: string }[];
  value: number;
  onChange: (value: number) => void;
};

/**
 * Quick-pick chips (choose, don't type): a row of toggle buttons with aria-pressed; the chosen chip shows a check
 * icon as well as its colour, so the state is never conveyed by colour alone.
 */
export function ChipGroup({ label, caption, options, value, onChange }: ChipGroupProps) {
  return (
    <div className="ui-chip-group" role="group" aria-label={label}>
      {caption && <span className="ui-chip-caption" aria-hidden="true">{caption}</span>}
      {options.map((option) => {
        const pressed = option.value === value;
        return (
          <button key={option.value} type="button" className={`ui-chip${pressed ? " is-pressed" : ""}`} aria-pressed={pressed}
            aria-label={option.name} title={option.name} onClick={() => onChange(option.value)}>
            {pressed && <Check aria-hidden="true" size={14} />}
            <span>{option.label}</span>
          </button>
        );
      })}
    </div>
  );
}
