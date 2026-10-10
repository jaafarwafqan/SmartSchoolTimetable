import { KeyRound, ShieldCheck } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { useMutation } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { apiRequest } from "../../api";
import { PasswordField } from "../../components/PasswordField";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { passwordMinLength } from "../../lib/credentialRules";
import { formatNumber } from "../../lib/format";
import { useFormFeedback, type FieldErrors } from "../../lib/useFormFeedback";
import { useUiStore } from "../../state/session";
import { useRefreshBootstrap } from "../auth/useBootstrap";
import { SettingsSection } from "./SettingsSection";

export function PasswordSection() {
  const refreshBootstrap = useRefreshBootstrap();
  const setLoginNotice = useUiStore((state) => state.setLoginNotice);
  const feedback = useFormFeedback();
  const changePassword = useMutation({
    mutationFn: (payload: { currentPassword: string; newPassword: string }) =>
      apiRequest<void>("/api/v1/auth/change-password", "POST", payload),
    onSuccess: async () => {
      // All sessions end; the confirmation is shown on the login screen.
      setLoginNotice(messages.app.passwordChanged);
      await refreshBootstrap();
    },
    onError: feedback.showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const currentPassword = String(form.get("currentPassword") ?? "");
    const newPassword = String(form.get("newPassword") ?? "");
    const errors: FieldErrors = {};
    if (!currentPassword) errors.CurrentPassword = messages.errors.REQUIRED;
    if (!newPassword) errors.NewPassword = messages.errors.REQUIRED;
    if (newPassword !== String(form.get("confirmPassword") ?? "")) {
      errors.ConfirmPassword = messages.app.validationConfirm;
    }
    if (Object.keys(errors).length > 0) {
      feedback.showFieldErrors(errors);
      return;
    }
    changePassword.mutate({ currentPassword, newPassword });
  }

  return (
    <SettingsSection
      id="password-section"
      icon={KeyRound}
      title={messages.app.passwordSectionTitle}
      description={messages.app.passwordSectionDescription}
    >
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <PasswordField
          id="currentPassword"
          label={messages.app.currentPassword}
          autoComplete="current-password"
          field="CurrentPassword"
          errors={feedback.fieldErrors}
        />
        <div className="form-grid">
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
        </div>
        <div className="form-actions">
          <Button type="submit" icon={<ShieldCheck aria-hidden="true" size={18} />} loading={changePassword.isPending}>
            {messages.app.changePassword}
          </Button>
        </div>
      </form>
    </SettingsSection>
  );
}
