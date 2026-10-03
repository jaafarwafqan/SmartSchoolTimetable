import { messages, type ApiErrorCode, type FieldName } from "./messages";

export type ApiFailure = {
  code?: string;
  correlationId?: string;
  errors?: Array<{ field: string; code: string }>;
};

export class ApiRequestError extends Error {
  readonly code: ApiErrorCode;
  readonly fields: Array<{ field: string; code: string }>;

  constructor(code: string, fields: Array<{ field: string; code: string }> = []) {
    super(code);
    this.code = code in messages.errors ? (code as ApiErrorCode) : "UNKNOWN_ERROR";
    this.fields = fields;
  }

  get arabicMessage(): string {
    return messages.errors[this.code];
  }
}

export function translatedFieldError(field: string, code: string): string {
  const label = field in messages.fields ? messages.fields[field as FieldName] : "";
  const detail = code in messages.errors
    ? messages.errors[code as ApiErrorCode]
    : messages.errors.VALIDATION_FAILED;
  return label ? `${label}: ${detail}` : detail;
}

export function isLatinFree(text: string): boolean {
  return !/[A-Za-z]/.test(text);
}
