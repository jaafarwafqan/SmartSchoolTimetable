import type { InputHTMLAttributes, ReactNode } from "react";

type CheckboxProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type" | "children"> & {
  children: ReactNode;
};

/** Checkbox with its visible label as the click target (whole row is clickable). */
export function Checkbox({ children, className = "", checked, ...props }: CheckboxProps) {
  return (
    <label className={`ui-checkbox${checked ? " is-checked" : ""} ${className}`.trim()}>
      <input {...props} type="checkbox" checked={checked} />
      <span>{children}</span>
    </label>
  );
}
