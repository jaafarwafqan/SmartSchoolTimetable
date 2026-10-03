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
          <Button variant={danger ? "danger" : "primary"} icon={confirmIcon} loading={loading} onClick={onConfirm}>
            {confirmLabel}
          </Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={onCancel}>
            {messages.app.cancel}
          </Button>
        </>
      )}
    />
  );
}
