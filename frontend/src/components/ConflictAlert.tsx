import { RefreshCw } from "lucide-react";
import { messages } from "../i18n/messages";
import { Alert } from "./ui/alert";
import { Button } from "./ui/button";

/**
 * Shown when a save is rejected with 409 CONFLICT because the record changed in another tab.
 * The owner reloads the latest version and re-applies their edit (nothing is overwritten silently).
 */
export function ConflictAlert({ onReload, loading = false }: { onReload: () => void; loading?: boolean }) {
  return (
    <Alert tone="warning">
      <strong>{messages.school.common.conflictTitle}</strong>
      <span>{messages.school.common.conflictBody}</span>
      <span>
        <Button size="sm" variant="secondary" icon={<RefreshCw aria-hidden="true" size={18} />} loading={loading} onClick={onReload}>
          {messages.school.common.reload}
        </Button>
      </span>
    </Alert>
  );
}
