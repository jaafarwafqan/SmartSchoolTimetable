import { useState } from "react";
import { userErrorMessage } from "../api";
import { ApiRequestError, isLatinFree, translatedFieldError } from "../i18n/errors";
import { messages } from "../i18n/messages";

export function useErrorText() {
  const [error, setError] = useState<string | null>(null);
  const showError = (reason: unknown) => {
    if (reason instanceof ApiRequestError && reason.fields.length > 0) {
      const details = reason.fields
        .map((item) => translatedFieldError(item.field, item.code))
        .filter(isLatinFree);
      setError(details.length > 0 ? details.join("، ") : messages.errors.VALIDATION_FAILED);
      return;
    }
    setError(userErrorMessage(reason));
  };
  return { error, setError, showError };
}
