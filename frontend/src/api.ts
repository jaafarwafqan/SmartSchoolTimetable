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
  const isForm = body instanceof FormData;
  if (body !== undefined && !isForm) headers.set("Content-Type", "application/json");
  // Every state-changing request carries the per-launch token, with or without a body.
  if (method !== "GET" && method !== "HEAD") headers.set("X-Local-Launch-Token", launchToken());

  let response: Response;
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
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

/** The file name of a download: RFC 5987 `filename*` (Arabic names) first, then `filename`. */
export function fileNameOf(disposition: string | null, fallback: string): string {
  if (!disposition) return fallback;
  const encoded = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(disposition);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      // A malformed name falls through to the plain one.
    }
  }
  const plain = /filename\s*=\s*"?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1].trim() : fallback;
}

/**
 * A POST that answers with a file (phone packages, the key copy): the per-launch token like any state-changing request, the error
 * envelope's code on failure, and the browser's download of the bytes on success. Returns the saved file name.
 */
export async function apiDownload(path: string, body: unknown, fallbackName: string): Promise<string> {
  let response: Response;
  try {
    response = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Local-Launch-Token": launchToken() },
      body: JSON.stringify(body ?? {}),
      credentials: "same-origin",
      cache: "no-store",
      signal: AbortSignal.timeout(60_000),
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "TimeoutError") throw new ApiRequestError("REQUEST_TIMEOUT");
    throw new ApiRequestError("NETWORK_ERROR");
  }
  if (!response.ok) {
    let payload: unknown = null;
    try {
      payload = await response.json();
    } catch {
      // No envelope: the generic message.
    }
    const failure: ApiFailure = isErrorEnvelope(payload) ? payload : {};
    throw new ApiRequestError(failure.code ?? "UNKNOWN_ERROR", failure.errors ?? []);
  }
  const name = fileNameOf(response.headers.get("Content-Disposition"), fallbackName);
  saveBlob(await response.blob(), name);
  return name;
}

/** Hands bytes to the browser's own download (the Downloads folder); nothing is written by the program itself. */
export function saveBlob(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = name;
  link.rel = "noopener";
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
