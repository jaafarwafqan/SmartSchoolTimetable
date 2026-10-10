import { useEffect } from "react";

export type ThemePreference = "system" | "light" | "dark";
export type ResolvedTheme = "light" | "dark";

/** The same key as public/theme-init.js, which applies it before the first paint. */
export const themeStorageKey = "sst-theme";

/** «حسب النظام» follows the operating system's setting; light and dark are fixed. */
export function resolveTheme(preference: ThemePreference, systemPrefersDark: boolean): ResolvedTheme {
  if (preference === "system") return systemPrefersDark ? "dark" : "light";
  return preference;
}

function systemPrefersDark(): boolean {
  return typeof window.matchMedia === "function" && window.matchMedia("(prefers-color-scheme: dark)").matches;
}

/** Sets data-theme on the page and remembers the choice in this browser (a per-viewer convenience: the database is the source of truth). */
export function applyTheme(preference: ThemePreference): ResolvedTheme {
  const resolved = resolveTheme(preference, systemPrefersDark());
  document.documentElement.setAttribute("data-theme", resolved);
  try {
    window.localStorage.setItem(themeStorageKey, preference);
  } catch {
    // Private windows and blocked storage: the theme still applies for this visit.
  }
  return resolved;
}

/** Applies the stored theme preference, and follows the system while it is «حسب النظام». */
export function useApplyTheme(preference: ThemePreference | undefined): void {
  useEffect(() => {
    if (preference === undefined) return undefined;
    applyTheme(preference);
    if (preference !== "system" || typeof window.matchMedia !== "function") return undefined;
    const query = window.matchMedia("(prefers-color-scheme: dark)");
    const follow = () => applyTheme("system");
    query.addEventListener("change", follow);
    return () => query.removeEventListener("change", follow);
  }, [preference]);
}
