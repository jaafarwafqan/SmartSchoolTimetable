import { afterEach, describe, expect, it, vi } from "vitest";
import { apiRequest } from "./api";
import { ApiRequestError } from "./i18n/errors";
import { messages } from "./i18n/messages";
import { bootstrapQueryKey, type Bootstrap } from "./lib/bootstrapQuery";
import { queryClient } from "./lib/queryClient";

function jsonResponse(body: unknown, status: number): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("apiRequest", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    queryClient.clear();
  });

  it("treats a 2xx response that carries an error code as a failure", async () => {
    vi.stubGlobal("fetch", vi.fn(async () =>
      jsonResponse({ code: "UNAUTHENTICATED", correlationId: "c-1", errors: [] }, 200)));

    const request = apiRequest<void>("/api/v1/auth/change-password", "POST", {});
    await expect(request).rejects.toBeInstanceOf(ApiRequestError);
    await expect(request).rejects.toMatchObject({ code: "UNAUTHENTICATED" });
  });

  it("maps an unregistered code in a 2xx response to the generic Arabic error", async () => {
    vi.stubGlobal("fetch", vi.fn(async () =>
      jsonResponse({ code: "unauthenticated", correlationId: "c-2", errors: [] }, 200)));

    const error = await apiRequest<void>("/api/v1/auth/change-password", "POST", {}).catch((reason: unknown) => reason);
    expect(error).toBeInstanceOf(ApiRequestError);
    expect((error as ApiRequestError).arabicMessage).toBe(messages.errors.UNKNOWN_ERROR);
  });

  it("returns successful payloads and sends the launch token from the bootstrap query cache", async () => {
    const bootstrap: Bootstrap = {
      setupRequired: false,
      authenticated: true,
      username: "owner",
      recoveryCodeAcknowledgementRequired: false,
      launchToken: "launch-token-from-cache",
      inactivityTimeoutMinutes: 30,
      inactivityTimeoutChoices: [5, 15, 30, 60],
    };
    queryClient.setQueryData(bootstrapQueryKey, bootstrap);
    const fetchMock = vi.fn<(path: string, init?: RequestInit) => Promise<Response>>(async () =>
      jsonResponse({ recoveryCode: "AAAAAAAA-BBBBBBBB-CCCCCCCC-DDDDDDDD" }, 200));
    vi.stubGlobal("fetch", fetchMock);

    const result = await apiRequest<{ recoveryCode: string }>(
      "/api/v1/auth/recovery-code/regenerate",
      "POST",
      { currentPassword: "x" },
    );

    expect(result.recoveryCode).toBe("AAAAAAAA-BBBBBBBB-CCCCCCCC-DDDDDDDD");
    const headers = new Headers(fetchMock.mock.calls[0]?.[1]?.headers);
    expect(headers.get("X-Local-Launch-Token")).toBe("launch-token-from-cache");
  });
});
