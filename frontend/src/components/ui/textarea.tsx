import type { TextareaHTMLAttributes } from "react";

/** Multi-line text control with the same border, focus and error states as Input (DESIGN_SYSTEM.md 6.2). */
export function Textarea({ className = "", rows = 3, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} rows={rows} className={`ui-input ui-textarea ${className}`.trim()} />;
}
