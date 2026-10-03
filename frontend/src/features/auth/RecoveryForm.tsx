import { ArrowRight, ShieldCheck } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { useMutation } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { apiRequest } from "../../api";
import { PasswordField } from "../../components/PasswordField";
import { TextField } from "../../components/TextField";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { passwordMinLength } from "../../lib/credentialRules";
import { formatNumber } from "../../lib/format";
import { useFormFeedback, type FieldErrors } from "../../lib/useFormFeedback";
import { useUiStore } from "../../state/session";
import { useStoreIssuedRecoveryCode, type RecoveryCodeResponse } from "./useRecoveryCode";

export function RecoveryForm() {
  const storeIssuedCode = useStoreIssuedRecoveryCode();
  const setRecoveryFormOpen = useUiStore((state) => state.setRecoveryFormOpen);
  const feedback = useFormFeedback();
  const recover = useMutation({
    mutationFn: (payload: { recoveryCode: string; newPassword: string }) =>
      apiRequest<RecoveryCodeResponse>("/api/v1/auth/recovery", "POST", payload),
    onSuccess: async (result) => {
      await storeIssuedCode(result.recoveryCode);
      // Close the form only after the signed-in state has loaded, so a later logout shows the login screen.
      setRecoveryFormOpen(false);
    },
    onError: feedback.showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const recoveryCode = String(form.get("recoveryCode") ?? "").trim();
    const newPassword = String(form.get("newPassword") ?? "");
    const errors: FieldErrors = {};
    if (!recoveryCode) errors.RecoveryCode = messages.errors.REQUIRED;
    if (!newPassword) errors.NewPassword = messages.errors.REQUIRED;
    if (newPassword !== String(form.get("confirmPassword") ?? "")) {
      errors.ConfirmPassword = messages.app.validationConfirm;
    }
    if (Object.keys(errors).length > 0) {
      feedback.showFieldErrors(errors);
      return;
    }
    recover.mutate({ recoveryCode, newPassword });
  }

  return (
    <AuthLayout title={messages.app.recoveryTitle}>
      <Alert tone="warning" message={messages.app.codeWarning} />
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <TextField
          id="recoveryCode"
          label={messages.app.recoveryCode}
          autoComplete="off"
          dir="ltr"
          className="code-input"
          required
          field="RecoveryCode"
          errors={feedback.fieldErrors}
        />
        <PasswordField
          id="newPassword"
          label={messages.app.newPassword}
          autoComplete="new-password"
          hint={messages.app.passwordHint(formatNumber(passwordMinLength))}
          field="NewPassword"
          errors={feedback.fieldErrors}
        />
        <PasswordField
          id="confirmPassword"
          label={messages.app.confirmPassword}
          autoComplete="new-password"
          field="ConfirmPassword"
          errors={feedback.fieldErrors}
        />
        <Button type="submit" block icon={<ShieldCheck aria-hidden="true" size={18} />} loading={recover.isPending}>
          {messages.app.resetPassword}
        </Button>
      </form>
      <div className="auth-footer-actions">
        <Button
          variant="ghost"
          icon={<ArrowRight aria-hidden="true" size={18} />}
          onClick={() => setRecoveryFormOpen(false)}
        >
          {messages.app.backToLogin}
        </Button>
      </div>
    </AuthLayout>
  );
}
