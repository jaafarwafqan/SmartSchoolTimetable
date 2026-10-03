import { ApiRequestError, type ApiFailure } from "./i18n/errors";
import { messages } from "./i18n/messages";
import { bootstrapQueryKey, type Bootstrap } from "./lib/bootstrapQuery";
import { queryClient } from "./lib/queryClient";

function launchToken(): string {
  return queryClient.getQueryData<Bootstrap>(bootstrapQueryKey)?.launchToken ?? "";
}

function isErrorEnvelope(payload: unknown): payload is ApiFailure & { code: string } {
  return typeof payload === "object" &&
    payload !== null &&
    "code" in payload &&
    typeof payload.code === "string";
}

export async function apiRequest<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const headers = new Headers();
  if (body !== undefined) {
    headers.set("Content-Type", "application/json");
    headers.set("X-Local-Launch-Token", launchToken());
  }

  let response: Response;
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      credentials: "same-origin",
      cache: "no-store",
      signal: AbortSignal.timeout(10_000),
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "TimeoutError") {
      throw new ApiRequestError("REQUEST_TIMEOUT");
    }
    throw new ApiRequestError("NETWORK_ERROR");
  }

  if (response.status === 204) return undefined as T;

  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    throw new ApiRequestError("UNKNOWN_ERROR");
  }

  // An error envelope is an error even if a defect delivered it with a 2xx status.
  if (!response.ok || isErrorEnvelope(payload)) {
    const failure: ApiFailure = isErrorEnvelope(payload) ? payload : {};
    throw new ApiRequestError(failure.code ?? "UNKNOWN_ERROR", failure.errors ?? []);
  }

  return payload as T;
}

export function userErrorMessage(error: unknown): string {
  if (error instanceof ApiRequestError) return error.arabicMessage;
  return messages.errors.UNKNOWN_ERROR;
}
