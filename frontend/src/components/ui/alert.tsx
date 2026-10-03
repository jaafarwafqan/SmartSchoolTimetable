import { CircleAlert, CircleCheck, Info, TriangleAlert, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { isLatinFree } from "../../i18n/errors";
import { messages } from "../../i18n/messages";

export type AlertTone = "error" | "success" | "warning" | "info";

const icons: Record<AlertTone, LucideIcon> = {
  error: CircleAlert,
  success: CircleCheck,
  warning: TriangleAlert,
  info: Info,
};

type AlertProps = {
  tone: AlertTone;
  /** Plain message; rendered only when non-empty. Error text is guarded against non-Arabic leaks. */
  message?: string | null;
  children?: ReactNode;
};

/**
 * DESIGN_SYSTEM.md 6.3: soft background + status colour + icon + text.
 * Errors use role="alert"; success, warning and info use role="status". Never nest alerts.
 */
export function Alert({ tone, message, children }: AlertProps) {
  if (!message && !children) return null;
  const Icon = icons[tone];
  const text = tone === "error" && message && !isLatinFree(message) ? messages.errors.UNKNOWN_ERROR : message;
  return (
    <div className={`ui-alert ui-alert-${tone}`} role={tone === "error" ? "alert" : "status"}>
      <Icon aria-hidden="true" size={20} strokeWidth={2} />
      <div className="ui-alert-body">
        {text && <span>{text}</span>}
        {children}
      </div>
    </div>
  );
}
