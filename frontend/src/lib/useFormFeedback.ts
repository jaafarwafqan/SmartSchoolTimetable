import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { userErrorMessage } from "../api";
import {
  ApiRequestError,
  fieldErrorMessage,
  isFieldName,
  isLatinFree,
  translatedFieldError,
} from "../i18n/errors";
import { messages, type FieldName } from "../i18n/messages";

export type FieldErrors = Partial<Record<FieldName, string>>;

/**
 * Per-form feedback: a summary alert, inline field errors (from the API or client checks),
 * and a success message. After invalid input, focus moves to the first invalid field
 * (DESIGN_SYSTEM.md 6.2); attach `formRef` to the form element.
 */
export function useFormFeedback() {
  const formRef = useRef<HTMLFormElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [focusRequest, setFocusRequest] = useState(0);
  /** True after a 409 CONFLICT: the record changed elsewhere and must be reloaded before editing again. */
  const [conflict, setConflict] = useState(false);

  useEffect(() => {
    if (focusRequest === 0) return;
    formRef.current?.querySelector<HTMLElement>("[aria-invalid=\"true\"]")?.focus();
  }, [focusRequest]);

  const reset = useCallback(() => {
    setError(null);
    setSuccess(null);
    setFieldErrors({});
    setConflict(false);
  }, []);

  const showFieldErrors = useCallback((errors: FieldErrors) => {
    setSuccess(null);
    setError(null);
    setFieldErrors(errors);
    setFocusRequest((count) => count + 1);
  }, []);

  const showSuccess = useCallback((message: string) => {
    setError(null);
    setFieldErrors({});
    setSuccess(message);
  }, []);

  const showError = useCallback((reason: unknown) => {
    setSuccess(null);
    if (reason instanceof ApiRequestError && reason.code === "CONFLICT") {
      setFieldErrors({});
      setError(null);
      setConflict(true);
      return;
    }
    if (reason instanceof ApiRequestError && reason.fields.length > 0) {
      const inline: FieldErrors = {};
      const unmatched: string[] = [];
      for (const { field, code } of reason.fields) {
        if (isFieldName(field)) inline[field] ??= fieldErrorMessage(code);
        else unmatched.push(translatedFieldError(field, code));
      }
      setFieldErrors(inline);
      setError([messages.errors.VALIDATION_FAILED, ...unmatched.filter(isLatinFree)].join(" "));
      setFocusRequest((count) => count + 1);
      return;
    }
    setFieldErrors({});
    setError(userErrorMessage(reason));
  }, []);

  /** Form `onInput` handler: hides a field's error as soon as the user edits that field. */
  const clearFieldFromEvent = useCallback((event: FormEvent<HTMLFormElement>) => {
    const target = event.target;
    if (!(target instanceof HTMLInputElement || target instanceof HTMLSelectElement)) return;
    const field = target.dataset.field;
    if (!field || !isFieldName(field)) return;
    setFieldErrors((current) => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
  }, []);

  return {
    formRef,
    conflict,
    error,
    success,
    fieldErrors,
    reset,
    showError,
    showFieldErrors,
    showSuccess,
    setError,
    clearFieldFromEvent,
  };
}
