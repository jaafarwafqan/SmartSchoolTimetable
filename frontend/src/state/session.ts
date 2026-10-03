import { create } from "zustand";

export type BootstrapState = {
  setupRequired: boolean;
  authenticated: boolean;
  username: string | null;
  recoveryCodeAcknowledgementRequired: boolean;
  launchToken: string;
  inactivityTimeoutMinutes: number | null;
};

type SessionStore = {
  bootstrap: BootstrapState | null;
  recoveryCode: string | null;
  setBootstrap: (bootstrap: BootstrapState) => void;
  setRecoveryCode: (recoveryCode: string | null) => void;
};

export const useSessionStore = create<SessionStore>((set) => ({
  bootstrap: null,
  recoveryCode: null,
  setBootstrap: (bootstrap) => set({ bootstrap }),
  setRecoveryCode: (recoveryCode) => set({ recoveryCode }),
}));
