/** Server state returned by GET /api/v1/bootstrap. It lives only in the TanStack Query cache. */
export type Bootstrap = {
  setupRequired: boolean;
  authenticated: boolean;
  username: string | null;
  recoveryCodeAcknowledgementRequired: boolean;
  launchToken: string;
  inactivityTimeoutMinutes: number | null;
};

export const bootstrapQueryKey = ["bootstrap"] as const;
export const bootstrapPath = "/api/v1/bootstrap";
