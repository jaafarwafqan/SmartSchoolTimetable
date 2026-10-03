import { UserRound } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { useMutation } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { apiRequest } from "../../api";
import { PasswordField } from "../../components/PasswordField";
import { TextField } from "../../components/TextField";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { passwordMinLength, usernameMaxLength, usernameMinLength } from "../../lib/credentialRules";
import { formatNumber } from "../../lib/format";
import { useFormFeedback, type FieldErrors } from "../../lib/useFormFeedback";
import { useStoreIssuedRecoveryCode, type RecoveryCodeResponse } from "./useRecoveryCode";

type SetupPayload = { username: string; password: string; confirmPassword: string };

function validateSetup({ username, password, confirmPassword }: SetupPayload): FieldErrors {
  const errors: FieldErrors = {};
  if (username.length < usernameMinLength || username.length > usernameMaxLength) {
    errors.Username = messages.app.validationUsername(formatNumber(usernameMinLength), formatNumber(usernameMaxLength));
  }
  if (password.length < passwordMinLength) errors.Password = messages.app.validationPassword(formatNumber(passwordMinLength));
  if (confirmPassword !== password) errors.ConfirmPassword = messages.app.validationConfirm;
  return errors;
}

export function SetupScreen() {
  const storeIssuedCode = useStoreIssuedRecoveryCode();
  const feedback = useFormFeedback();
  const setup = useMutation({
    mutationFn: (payload: SetupPayload) =>
      apiRequest<RecoveryCodeResponse>("/api/v1/auth/setup", "POST", payload),
    onSuccess: (result) => storeIssuedCode(result.recoveryCode),
    onError: feedback.showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const payload: SetupPayload = {
      username: String(form.get("username") ?? "").trim(),
      password: String(form.get("password") ?? ""),
      confirmPassword: String(form.get("confirmPassword") ?? ""),
    };
    const errors = validateSetup(payload);
    if (Object.keys(errors).length > 0) {
      feedback.showFieldErrors(errors);
      return;
    }
    setup.mutate(payload);
  }

  return (
    <AuthLayout title={messages.app.setupTitle} description={messages.app.setupDescription}>
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <TextField
          id="username"
          label={messages.app.username}
          autoComplete="username"
          minLength={usernameMinLength}
          maxLength={usernameMaxLength}
          required
          hint={messages.app.usernameHint(formatNumber(usernameMinLength), formatNumber(usernameMaxLength))}
          field="Username"
          errors={feedback.fieldErrors}
        />
        <PasswordField
          id="password"
          label={messages.app.password}
          autoComplete="new-password"
          hint={messages.app.passwordHint(formatNumber(passwordMinLength))}
          field="Password"
          errors={feedback.fieldErrors}
        />
        <PasswordField
          id="confirmPassword"
          label={messages.app.confirmPassword}
          autoComplete="new-password"
          field="ConfirmPassword"
          errors={feedback.fieldErrors}
        />
        <Button type="submit" block icon={<UserRound aria-hidden="true" size={18} />} loading={setup.isPending}>
          {messages.app.createAccount}
        </Button>
      </form>
    </AuthLayout>
  );
}
