import { AlertCircle } from "lucide-react";
import { isLatinFree } from "../i18n/errors";
import { messages } from "../i18n/messages";

export function AlertMessage({ message }: { message: string | null }) {
  if (!message) return null;
  const safeMessage = isLatinFree(message) ? message : messages.errors.UNKNOWN_ERROR;
  return (
    <div className="alert-message" role="alert">
      <AlertCircle aria-hidden="true" size={20} strokeWidth={2} />
      <span>{safeMessage}</span>
    </div>
  );
}
