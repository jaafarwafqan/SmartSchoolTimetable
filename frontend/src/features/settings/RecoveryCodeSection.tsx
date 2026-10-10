import { LifeBuoy, RefreshCw } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import type { FormEvent } from "react";
import { PasswordField } from "../../components/PasswordField";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useRegenerateRecoveryCode } from "../auth/useRecoveryCode";
import { SettingsSection } from "./SettingsSection";

export function RecoveryCodeSection() {
  const feedback = useFormFeedback();
  const regenerate = useRegenerateRecoveryCode(feedback.showError);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const currentPassword = String(new FormData(event.currentTarget).get("recoveryCurrentPassword") ?? "");
    if (!currentPassword) {
      feedback.showFieldErrors({ CurrentPassword: messages.errors.REQUIRED });
      return;
    }
    regenerate.mutate(currentPassword);
  }

  return (
    <SettingsSection
      id="recovery-section"
      icon={LifeBuoy}
      title={messages.app.recoverySectionTitle}
      description={messages.app.recoverySectionDescription}
    >
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <PasswordField
          id="recoveryCurrentPassword"
          label={messages.app.currentPassword}
          autoComplete="current-password"
          field="CurrentPassword"
          errors={feedback.fieldErrors}
        />
        <div className="form-actions">
          <Button type="submit" icon={<RefreshCw aria-hidden="true" size={18} />} loading={regenerate.isPending}>
            {messages.app.generateNewCode}
          </Button>
        </div>
      </form>
    </SettingsSection>
  );
}
