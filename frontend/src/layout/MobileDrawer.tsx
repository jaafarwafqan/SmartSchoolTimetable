import { X } from "lucide-react";
import { useEffect, useRef } from "react";
import { IconButton } from "../components/ui/icon-button";
import { messages } from "../i18n/messages";
import { Sidebar } from "./Sidebar";

/**
 * Below 768px the sidebar becomes a modal drawer (native <dialog>: focus is trapped, Esc closes,
 * focus returns to the menu button).
 */
export function MobileDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const ref = useRef<HTMLDialogElement>(null);
  const returnFocusTo = useRef<HTMLElement | null>(null);

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
      className="app-drawer"
      aria-label={messages.school.nav.sidebarLabel}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div className="app-drawer-header">
        <IconButton
          aria-label={messages.school.nav.closeMenu}
          title={messages.school.nav.closeMenu}
          icon={<X aria-hidden="true" size={20} />}
          onClick={onClose}
        />
      </div>
      <Sidebar collapsed={false} onNavigate={onClose} />
    </dialog>
  );
}
