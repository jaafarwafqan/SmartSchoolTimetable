import { CircleCheck } from "lucide-react";
import type { ReactNode } from "react";

export type Choice<T extends string> = { value: T; label: string; description?: string; icon?: ReactNode };

type ChoiceCardsProps<T extends string> = {
  name: string;
  legend: string;
  choices: readonly Choice<T>[];
  value: T | null;
  onChange: (value: T) => void;
};

/**
 * Selection by cards (spec 2.5 §5: choose, don't type). A native radio group, so arrow keys move between cards;
 * the chosen card shows a check icon and text weight, never colour alone.
 */
export function ChoiceCards<T extends string>({ name, legend, choices, value, onChange }: ChoiceCardsProps<T>) {
  return (
    <fieldset className="ui-choice-cards">
      <legend>{legend}</legend>
      <div className="ui-choice-grid">
        {choices.map((choice) => {
          const checked = choice.value === value;
          return (
            <label key={choice.value} className={`ui-choice-card${checked ? " is-checked" : ""}`}>
              <input type="radio" name={name} value={choice.value} checked={checked} onChange={() => onChange(choice.value)} />
              <span className="ui-choice-icon" aria-hidden="true">{checked ? <CircleCheck size={20} /> : choice.icon}</span>
              <span className="ui-choice-text">
                <strong>{choice.label}</strong>
                {choice.description && <span>{choice.description}</span>}
              </span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
