import type { LucideIcon } from "lucide-react";
import { useEffect, useRef } from "react";
import { NavLink, useLocation } from "react-router-dom";

type TabNavProps = { label: string; tabs: readonly { to: string; label: string; icon: LucideIcon }[] };

/** Route tabs inside a navigation group; the current tab carries aria-current="page" (NavLink). */
export function TabNav({ label, tabs }: TabNavProps) {
  const { pathname } = useLocation();
  const list = useRef<HTMLUListElement>(null);
  // On a phone the tabs sit in one scrolling row: keep the current tab in view.
  // Again once the font has loaded: the tabs change width then, and the first position would be off.
  // Only the tab row scrolls (never the page): scrollIntoView also moved the window on a resize, which shifted the page while a full-page
  // screenshot was taken (CI) and could jump the page under the owner's eyes.
  useEffect(() => {
    const show = () => {
      const row = list.current;
      const active = row?.querySelector<HTMLElement>(".is-active");
      if (!row || !active || row.scrollWidth <= row.clientWidth) return;
      const rowBox = row.getBoundingClientRect();
      const tabBox = active.getBoundingClientRect();
      row.scrollLeft += tabBox.left + tabBox.width / 2 - (rowBox.left + rowBox.width / 2);
    };
    show();
    void document.fonts?.ready.then(show);
    window.addEventListener("resize", show);
    return () => window.removeEventListener("resize", show);
  }, [pathname]);
  return (
    <nav className="ui-tabs" aria-label={label}>
      <ul ref={list}>
        {tabs.map((tab) => (
          <li key={tab.to}>
            <NavLink to={tab.to} className={({ isActive }) => `ui-tab${isActive ? " is-active" : ""}`}>
              <tab.icon aria-hidden="true" size={18} />
              <span>{tab.label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
