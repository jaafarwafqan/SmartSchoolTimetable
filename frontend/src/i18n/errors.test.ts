import { describe, expect, it } from "vitest";
import { ApiRequestError, isLatinFree, translatedFieldError } from "./errors";
import { messages } from "./messages";

describe("Arabic error localization", () => {
  it("maps unknown and missing codes to a generic Arabic message", () => {
    expect(new ApiRequestError("UNLISTED_SERVER_CODE").arabicMessage).toBe(messages.errors.UNKNOWN_ERROR);
    expect(new ApiRequestError("").arabicMessage).toBe(messages.errors.UNKNOWN_ERROR);
  });

  it("localizes validation field and code without Latin text", () => {
    const message = translatedFieldError("ConfirmPassword", "PASSWORD_MISMATCH");
    expect(message).toContain(messages.fields.ConfirmPassword);
    expect(message).toContain(messages.errors.PASSWORD_MISMATCH);
    expect(isLatinFree(message)).toBe(true);
  });

  it("keeps every visible error string Arabic", () => {
    for (const error of Object.values(messages.errors)) {
      expect(isLatinFree(error)).toBe(true);
    }
  });
});
