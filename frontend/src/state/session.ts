import { create } from "zustand";

/**
 * UI-only state. Server state (bootstrap, school data) lives in the TanStack Query cache.
 * The recovery code is kept in memory only and is never persisted.
 */
type UiStore = {
  recoveryCode: string | null;
  recoveryFormOpen: boolean;
  /** One-time confirmation shown on the login screen (for example after a password change). */
  loginNotice: string | null;
  /** Username kept after "lock screen" so the login form can be pre-filled. */
  lockedUsername: string | null;
  sidebarCollapsed: boolean;
  setRecoveryCode: (recoveryCode: string | null) => void;
  setRecoveryFormOpen: (open: boolean) => void;
  setLoginNotice: (notice: string | null) => void;
  setLockedUsername: (username: string | null) => void;
  toggleSidebar: () => void;
};

export const useUiStore = create<UiStore>((set) => ({
  recoveryCode: null,
  recoveryFormOpen: false,
  loginNotice: null,
  lockedUsername: null,
  sidebarCollapsed: false,
  setRecoveryCode: (recoveryCode) => set({ recoveryCode }),
  setRecoveryFormOpen: (recoveryFormOpen) => set({ recoveryFormOpen }),
  setLoginNotice: (loginNotice) => set({ loginNotice }),
  setLockedUsername: (lockedUsername) => set({ lockedUsername }),
  toggleSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
}));
