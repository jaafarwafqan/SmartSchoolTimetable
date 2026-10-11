import { useMutation } from "@tanstack/react-query";
import { UserRound, UserRoundPen } from "lucide-react";
import type { FormEvent } from "react";
import { apiRequest } from "../../api";
import { PasswordField } from "../../components/PasswordField";
import { TextField } from "../../components/TextField";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { usernameMaxLength, usernameMinLength } from "../../lib/credentialRules";
import { formatNumber } from "../../lib/format";
import { useFormFeedback, type FieldErrors } from "../../lib/useFormFeedback";
import { useRefreshBootstrap } from "../auth/useBootstrap";
import { SettingsSection } from "./SettingsSection";

const text = messages.app;

/** M2: change the username with the current password; the session stays open and shows the new name. */
export function UsernameSection({ username }: { username: string | null }) {
  const refreshBootstrap = useRefreshBootstrap();
  const feedback = useFormFeedback();
  const change = useMutation({
    mutationFn: (payload: { currentPassword: string; newUsername: string }) => apiRequest<void>("/api/v1/auth/change-username", "POST", payload),
    onSuccess: async () => {
      feedback.showSuccess(text.usernameChanged);
      await refreshBootstrap();
    },
    onError: feedback.showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    const form = new FormData(event.currentTarget);
    const currentPassword = String(form.get("usernameCurrentPassword") ?? "");
    const newUsername = String(form.get("newUsername") ?? "");
    const errors: FieldErrors = {};
    if (!currentPassword) errors.CurrentPassword = messages.errors.REQUIRED;
    if (!newUsername.trim()) errors.NewUsername = messages.errors.REQUIRED;
    if (Object.keys(errors).length > 0) {
      feedback.showFieldErrors(errors);
      return;
    }
    change.mutate({ currentPassword, newUsername });
  }

  return (
    <SettingsSection id="username-section" icon={UserRound} title={text.usernameSectionTitle} description={text.usernameSectionDescription}>
      {username && (
        <dl className="info-row">
          <dt>{text.usernameCurrent}</dt>
          <dd><Badge tone="primary" icon={<UserRound aria-hidden="true" size={16} />}>{username}</Badge></dd>
        </dl>
      )}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <TextField id="newUsername" label={text.newUsername} autoComplete="username" dir="ltr" maxLength={usernameMaxLength} required field="NewUsername" errors={feedback.fieldErrors}
          hint={text.usernameHint(formatNumber(usernameMinLength), formatNumber(usernameMaxLength))} />
        <PasswordField id="usernameCurrentPassword" label={text.currentPassword} autoComplete="current-password" field="CurrentPassword" errors={feedback.fieldErrors} />
        <div className="form-actions">
          <Button type="submit" icon={<UserRoundPen aria-hidden="true" size={18} />} loading={change.isPending}>{text.changeUsername}</Button>
        </div>
      </form>
    </SettingsSection>
  );
}
