import { ChevronDown } from "lucide-react";
import type { SelectHTMLAttributes } from "react";

export type SelectOption = { value: string; label: string };

type SelectProps = Omit<SelectHTMLAttributes<HTMLSelectElement>, "children"> & {
  options: readonly SelectOption[];
};

export function Select({ options, className = "", ...props }: SelectProps) {
  return (
    <span className="ui-select-control">
      <select {...props} className={`ui-input ui-select ${className}`.trim()}>
        {options.map((option) => (
          <option key={option.value} value={option.value}>{option.label}</option>
        ))}
      </select>
      <ChevronDown className="ui-select-chevron" aria-hidden="true" size={18} strokeWidth={2} />
    </span>
  );
}
