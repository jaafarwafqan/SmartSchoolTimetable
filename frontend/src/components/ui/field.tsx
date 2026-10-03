import { CircleAlert } from "lucide-react";
import type { ReactNode } from "react";
import { messages } from "../../i18n/messages";

type FieldProps = {
  id: string;
  label: string;
  required?: boolean;
  hint?: string;
  error?: string;
  children: ReactNode;
};

/** The id of the element that describes the control: the error when present, otherwise the hint. */
export function fieldDescribedBy(id: string, hint?: string, error?: string): string | undefined {
  if (error) return `${id}-error`;
  if (hint) return `${id}-hint`;
  return undefined;
}

/**
 * DESIGN_SYSTEM.md 6.2: label above the control, "(مطلوب)" in text for required fields,
 * hint below in ink-subtle, error below with icon in danger text.
 */
export function Field({ id, label, required = false, hint, error, children }: FieldProps) {
  return (
    <div className="ui-field">
      {/* The marker sits outside <label> so the accessible name stays the plain label; the input's
          required attribute announces the requirement to screen readers. */}
      <div className="ui-field-label-row">
        <label htmlFor={id}>{label}</label>
        {required && <span className="ui-field-required" aria-hidden="true">{messages.app.requiredMarker}</span>}
      </div>
      {children}
      {hint && !error && <p id={`${id}-hint`} className="ui-field-hint">{hint}</p>}
      {error && (
        <p id={`${id}-error`} className="ui-field-error">
          <CircleAlert aria-hidden="true" size={16} strokeWidth={2} />
          <span>{error}</span>
        </p>
      )}
    </div>
  );
}
