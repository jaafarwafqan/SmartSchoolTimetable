import { messages, type ApiErrorCode, type FieldName } from "./messages";

export type ApiFailure = {
  code?: string;
  correlationId?: string;
  errors?: Array<{ field: string; code: string }>;
};

function isApiErrorCode(code: string): code is ApiErrorCode {
  return Object.hasOwn(messages.errors, code);
}

function isFieldName(field: string): field is FieldName {
  return Object.hasOwn(messages.fields, field);
}

export class ApiRequestError extends Error {
  readonly code: ApiErrorCode;
  readonly fields: Array<{ field: string; code: string }>;

  constructor(code: string, fields: Array<{ field: string; code: string }> = []) {
    super(code);
    this.code = isApiErrorCode(code) ? code : "UNKNOWN_ERROR";
    this.fields = fields;
  }

  get arabicMessage(): string {
    return messages.errors[this.code];
  }
}

export function translatedFieldError(field: string, code: string): string {
  const label = isFieldName(field) ? messages.fields[field] : "";
  const detail = isApiErrorCode(code) ? messages.errors[code] : messages.errors.VALIDATION_FAILED;
  return label ? `${label}: ${detail}` : detail;
}

export function isLatinFree(text: string): boolean {
  return !/[A-Za-z]/.test(text);
}
