import { Check } from "lucide-react";
import { subjectColorClasses, type SubjectColorIndex } from "./timetable-cell";

const indexes = Object.keys(subjectColorClasses).map(Number) as SubjectColorIndex[];

type SubjectColorPickerProps = {
  name: string;
  legend: string;
  value: number;
  /** Accessible name of each swatch, e.g. "اللون 3". */
  swatchLabel: (index: number) => string;
  onChange: (index: SubjectColorIndex) => void;
};

/**
 * DESIGN_SYSTEM.md 2: subjects choose only from the ten palette tokens. A native radio group, so arrow keys move
 * between swatches; the chosen swatch shows a check icon, so the state is not conveyed by colour alone.
 */
export function SubjectColorPicker({ name, legend, value, swatchLabel, onChange }: SubjectColorPickerProps) {
  return (
    <fieldset className="ui-color-picker">
      <legend>{legend}</legend>
      <div className="ui-color-picker-swatches">
        {indexes.map((index) => (
          <label key={index} className={`ui-color-swatch ${subjectColorClasses[index]}${value === index ? " is-selected" : ""}`} title={swatchLabel(index)}>
            <input type="radio" name={name} value={index} checked={value === index} aria-label={swatchLabel(index)} onChange={() => onChange(index)} />
            {value === index && <Check aria-hidden="true" size={18} />}
          </label>
        ))}
      </div>
    </fieldset>
  );
}
