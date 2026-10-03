import { LogOut, RefreshCw } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import type { FormEvent } from "react";
import { PasswordField } from "../../components/PasswordField";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useLogout } from "./useLogout";
import { useRegenerateRecoveryCode } from "./useRecoveryCode";

/**
 * Blocking screen shown when the owner is signed in, the server reports an unacknowledged recovery code,
 * and the code is no longer in memory (for example after a reload). The application is not reachable
 * until a replacement code is generated with the current password and acknowledged.
 */
export function RecoveryPendingScreen() {
  const feedback = useFormFeedback();
  const regenerate = useRegenerateRecoveryCode(feedback.showError);
  const logout = useLogout(feedback.showError);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const currentPassword = String(form.get("pendingCurrentPassword") ?? "");
    if (!currentPassword) {
      feedback.showFieldErrors({ CurrentPassword: messages.errors.REQUIRED });
      return;
    }
    regenerate.mutate(currentPassword);
  }

  return (
    <AuthLayout title={messages.app.recoveryPendingTitle}>
      <Alert tone="warning" message={messages.app.recoveryPendingDescription} />
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <PasswordField
          id="pendingCurrentPassword"
          label={messages.app.currentPassword}
          autoComplete="current-password"
          field="CurrentPassword"
          errors={feedback.fieldErrors}
        />
        <Button type="submit" block icon={<RefreshCw aria-hidden="true" size={18} />} loading={regenerate.isPending}>
          {messages.app.generateCode}
        </Button>
      </form>
      <div className="auth-footer-actions">
        <Button
          variant="ghost"
          icon={<LogOut aria-hidden="true" size={18} />}
          loading={logout.isPending}
          onClick={() => {
            feedback.reset();
            logout.mutate();
          }}
        >
          {messages.app.logout}
        </Button>
      </div>
    </AuthLayout>
  );
}
