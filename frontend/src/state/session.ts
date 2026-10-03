import { create } from "zustand";

/**
 * UI-only state. Server state (bootstrap/session) lives in the TanStack Query cache.
 * The recovery code is kept in memory only and is never persisted.
 */
type UiStore = {
  recoveryCode: string | null;
  recoveryFormOpen: boolean;
  /** One-time confirmation shown on the login screen (for example after a password change). */
  loginNotice: string | null;
  setRecoveryCode: (recoveryCode: string | null) => void;
  setRecoveryFormOpen: (open: boolean) => void;
  setLoginNotice: (notice: string | null) => void;
};

export const useUiStore = create<UiStore>((set) => ({
  recoveryCode: null,
  recoveryFormOpen: false,
  loginNotice: null,
  setRecoveryCode: (recoveryCode) => set({ recoveryCode }),
  setRecoveryFormOpen: (recoveryFormOpen) => set({ recoveryFormOpen }),
  setLoginNotice: (loginNotice) => set({ loginNotice }),
}));
