import { create } from "zustand";

/**
 * UI-only state. Server state (bootstrap/session) lives in the TanStack Query cache.
 * The recovery code is kept in memory only and is never persisted.
 */
type UiStore = {
  recoveryCode: string | null;
  recoveryFormOpen: boolean;
  setRecoveryCode: (recoveryCode: string | null) => void;
  setRecoveryFormOpen: (open: boolean) => void;
};

export const useUiStore = create<UiStore>((set) => ({
  recoveryCode: null,
  recoveryFormOpen: false,
  setRecoveryCode: (recoveryCode) => set({ recoveryCode }),
  setRecoveryFormOpen: (recoveryFormOpen) => set({ recoveryFormOpen }),
}));
