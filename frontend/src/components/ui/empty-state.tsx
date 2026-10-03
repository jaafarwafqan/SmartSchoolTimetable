import type { ReactNode } from "react";

type EmptyStateProps = {
  icon: ReactNode;
  message: string;
  /** Primary action (DESIGN_SYSTEM.md 6.4: icon + sentence + primary action). */
  action?: ReactNode;
};

export function EmptyState({ icon, message, action }: EmptyStateProps) {
  return (
    <div className="ui-empty-state">
      {icon}
      <p>{message}</p>
      {action}
    </div>
  );
}
