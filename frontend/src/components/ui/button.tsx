import type { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  children: ReactNode;
  icon: ReactNode;
  variant?: "primary" | "secondary" | "ghost" | "danger";
};

export function Button({
  children,
  className = "",
  icon,
  variant = "primary",
  ...props
}: ButtonProps) {
  return (
    <button className={`ui-button ui-button-${variant} ${className}`} {...props}>
      {icon}
      <span>{children}</span>
    </button>
  );
}
