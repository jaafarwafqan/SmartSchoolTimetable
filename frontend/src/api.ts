import { ApiRequestError, type ApiFailure } from "./i18n/errors";
import { messages } from "./i18n/messages";
import { useSessionStore } from "./state/session";

export async function apiRequest<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const headers = new Headers();
  if (body !== undefined) {
    headers.set("Content-Type", "application/json");
    headers.set("X-Local-Launch-Token", useSessionStore.getState().bootstrap?.launchToken ?? "");
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

  if (!response.ok) {
    let failure: ApiFailure = {};
    try {
      failure = (await response.json()) as ApiFailure;
    } catch {
      throw new ApiRequestError("UNKNOWN_ERROR");
    }
    throw new ApiRequestError(failure.code ?? "UNKNOWN_ERROR", failure.errors ?? []);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export function userErrorMessage(error: unknown): string {
  if (error instanceof ApiRequestError) return error.arabicMessage;
  return messages.errors.UNKNOWN_ERROR;
}
