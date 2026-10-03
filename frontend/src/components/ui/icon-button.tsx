import type { ButtonHTMLAttributes, ReactNode } from "react";

type IconButtonProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "aria-label" | "title"> & {
  /** Arabic accessible name; also used as the tooltip (DESIGN_SYSTEM.md 5). */
  "aria-label": string;
  title: string;
  icon: ReactNode;
};

/** Icon-only control. Allowed only for show/hide password, close, and dense-table row actions. */
export function IconButton({ icon, className = "", type = "button", ...props }: IconButtonProps) {
  return (
    <button {...props} type={type} className={`ui-icon-button ${className}`.trim()}>
      {icon}
    </button>
  );
}
