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
  useEffect(() => {
    const show = () => list.current?.querySelector<HTMLElement>(".is-active")?.scrollIntoView?.({ block: "nearest", inline: "center" });
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
