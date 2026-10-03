import type { ReactNode } from "react";

type BadgeProps = {
  tone?: "neutral" | "primary" | "success" | "warning" | "danger";
  /** Status badges carry an icon so meaning is never conveyed by colour alone. */
  icon?: ReactNode;
  children: ReactNode;
};

export function Badge({ tone = "neutral", icon, children }: BadgeProps) {
  return (
    <span className={`ui-badge ui-badge-${tone}`}>
      {icon}
      <span>{children}</span>
    </span>
  );
}
