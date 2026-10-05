import { X } from "lucide-react";
import type { ReactNode } from "react";
import { messages } from "../../i18n/messages";
import { Button } from "./button";
import { Dialog } from "./dialog";

type ConfirmDialogProps = {
  open: boolean;
  title: string;
  /** One sentence stating the consequence (DESIGN_SYSTEM.md 6.3). */
  consequence: string;
  confirmLabel: string;
  confirmIcon: ReactNode;
  danger?: boolean;
  loading?: boolean;
  /** The action cannot run (for example, other records still depend on the target). */
  confirmDisabled?: boolean;
  /** Extra content under the consequence, such as the list of dependent records. */
  children?: ReactNode;
  onConfirm: () => void;
  onCancel: () => void;
};

export function ConfirmDialog({
  open,
  title,
  consequence,
  confirmLabel,
  confirmIcon,
  danger = false,
  loading = false,
  confirmDisabled = false,
  children,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Dialog
      open={open}
      title={title}
      description={consequence}
      onClose={onCancel}
      footer={(
        <>
          <Button variant={danger ? "danger" : "primary"} icon={confirmIcon} loading={loading} disabled={confirmDisabled} onClick={onConfirm}>
            {confirmLabel}
          </Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={onCancel}>
            {messages.app.cancel}
          </Button>
        </>
      )}
    >
      {children}
    </Dialog>
  );
}
