import { useEffect, useRef } from "react";
import { Sidebar } from "./Sidebar";

export const mobileMenuId = "app-mobile-menu";

/**
 * Below 768px the navigation opens IN THE PAGE FLOW under the top bar (no overlay, no edge-anchored panel:
 * ADR 0024). Esc closes it and returns focus to the menu button; choosing a link closes it.
 */
export function MobileMenu({ open, onClose, returnFocusTo }: { open: boolean; onClose: () => void; returnFocusTo: () => HTMLElement | null }) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    ref.current?.querySelector<HTMLElement>("a")?.focus();
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "Escape") return;
      onClose();
      returnFocusTo()?.focus();
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, onClose, returnFocusTo]);

  if (!open) return null;
  return (
    <div id={mobileMenuId} ref={ref} className="app-mobile-menu">
      <Sidebar collapsed={false} onNavigate={onClose} />
    </div>
  );
}
