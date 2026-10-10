import type { ReactNode } from "react";

type BadgeProps = (
  | { tone?: "neutral"; icon?: ReactNode }
  /** Meaning must never rely on colour alone (DESIGN_SYSTEM.md): every non-neutral badge needs an icon. */
  | { tone: "primary" | "success" | "warning" | "danger"; icon: ReactNode }
) & { children: ReactNode };

export function Badge({ tone = "neutral", icon, children }: BadgeProps) {
  return (
    <span className={`ui-badge ui-badge-${tone}`}>
      {icon}
      <span>{children}</span>
    </span>
  );
}
