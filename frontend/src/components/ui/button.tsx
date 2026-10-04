import type { ButtonHTMLAttributes, ReactNode, Ref } from "react";
import { Spinner } from "./spinner";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  ref?: Ref<HTMLButtonElement>;
  children: ReactNode;
  icon: ReactNode;
  variant?: "primary" | "secondary" | "ghost" | "danger";
  size?: "sm" | "md" | "lg";
  /** Shows a spinner in place of the icon and disables the button while an action runs. */
  loading?: boolean;
  /** Stretches the button to the full width of its container. */
  block?: boolean;
};

/** DESIGN_SYSTEM.md 6.1: always icon + label; the icon sits at the start side (right in RTL). */
export function Button({
  children,
  className = "",
  icon,
  variant = "primary",
  size = "md",
  loading = false,
  block = false,
  disabled,
  type = "button",
  ...props
}: ButtonProps) {
  const classes = [
    "ui-button",
    `ui-button-${variant}`,
    `ui-button-${size}`,
    block ? "ui-button-block" : "",
    className,
  ].filter(Boolean).join(" ");
  return (
    <button
      {...props}
      type={type}
      className={classes}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
    >
      {loading ? <Spinner size={18} /> : icon}
      <span>{children}</span>
    </button>
  );
}
