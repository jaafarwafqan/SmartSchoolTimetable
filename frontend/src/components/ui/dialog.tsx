import { X } from "lucide-react";
import { useEffect, useId, useRef, type ReactNode } from "react";
import { messages } from "../../i18n/messages";
import { IconButton } from "./icon-button";

type DialogProps = {
  open: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  /** Footer actions: primary first (right in RTL), then secondary. */
  footer: ReactNode;
  children?: ReactNode;
};

/**
 * Modal dialog on the native <dialog> element: the browser traps focus and closes on Esc.
 * Focus returns to the element that opened it (DESIGN_SYSTEM.md 9).
 */
export function Dialog({ open, title, description, onClose, footer, children }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const returnFocusTo = useRef<HTMLElement | null>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) {
      returnFocusTo.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
      returnFocusTo.current?.focus();
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      className="ui-dialog"
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
    >
      <header className="ui-dialog-header">
        <h2 id={titleId}>{title}</h2>
        <IconButton
          aria-label={messages.app.close}
          title={messages.app.close}
          icon={<X aria-hidden="true" size={20} />}
          onClick={onClose}
        />
      </header>
      {description && <p id={descriptionId} className="ui-dialog-description">{description}</p>}
      {children}
      <footer className="ui-dialog-footer">{footer}</footer>
    </dialog>
  );
}
