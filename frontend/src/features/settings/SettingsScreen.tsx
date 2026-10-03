import { RefreshCw, ShieldCheck } from "lucide-react";
import { useMutation } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { apiRequest } from "../../api";
import { AlertMessage } from "../../components/AlertMessage";
import { PasswordField } from "../../components/PasswordField";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { messages } from "../../i18n/messages";
import type { Bootstrap } from "../../lib/bootstrapQuery";
import { useErrorText } from "../../lib/useErrorText";
import { useRefreshBootstrap } from "../auth/useBootstrap";
import { useRegenerateRecoveryCode } from "../auth/useRecoveryCode";

export function SettingsScreen({ bootstrap }: { bootstrap: Bootstrap }) {
  const refreshBootstrap = useRefreshBootstrap();
  const { error, setError, showError } = useErrorText();
  const [passwordMessage, setPasswordMessage] = useState<string | null>(null);
  const regenerate = useRegenerateRecoveryCode(showError);
  const changePassword = useMutation({
    mutationFn: (payload: { currentPassword: string; newPassword: string }) =>
      apiRequest<void>("/api/v1/auth/change-password", "POST", payload),
    onSuccess: async () => {
      setPasswordMessage(messages.app.passwordChanged);
      setError(null);
      await refreshBootstrap();
    },
    onError: showError,
  });

  function submitRecovery(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    const form = new FormData(event.currentTarget);
    regenerate.mutate(String(form.get("recoveryCurrentPassword") ?? ""));
  }

  function submitPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setPasswordMessage(null);
    const form = new FormData(event.currentTarget);
    const newPassword = String(form.get("newPassword") ?? "");
    if (newPassword !== String(form.get("confirmPassword") ?? "")) {
      setError(messages.app.validationConfirm);
      return;
    }
    changePassword.mutate({
      currentPassword: String(form.get("currentPassword") ?? ""),
      newPassword,
    });
  }

  const inactivityLabel = useMemo(
    () => bootstrap.inactivityTimeoutMinutes === null
      ? messages.app.neverLock
      : messages.app.timeoutMinutes(bootstrap.inactivityTimeoutMinutes),
    [bootstrap.inactivityTimeoutMinutes],
  );

  return (
    <Card className="settings-card">
      <h2>{messages.app.settings}</h2>
      <AlertMessage message={error} />
      {passwordMessage && <p className="success-message" role="status">{passwordMessage}</p>}
      <p>{messages.app.inactivitySummary(inactivityLabel)}</p>
      <h3>{messages.app.generateCode}</h3>
      <form noValidate onSubmit={submitRecovery}>
        <PasswordField id="recoveryCurrentPassword" label={messages.app.currentPassword} autoComplete="current-password" />
        <Button type="submit" icon={<RefreshCw aria-hidden="true" size={20} />} disabled={regenerate.isPending}>
          {messages.app.generateCode}
        </Button>
      </form>
      <h3>{messages.app.changePassword}</h3>
      <form noValidate onSubmit={submitPassword}>
        <PasswordField id="currentPassword" label={messages.app.currentPassword} autoComplete="current-password" />
        <PasswordField id="newPassword" label={messages.app.newPassword} autoComplete="new-password" />
        <PasswordField id="confirmPassword" label={messages.app.confirmPassword} autoComplete="new-password" />
        <Button type="submit" icon={<ShieldCheck aria-hidden="true" size={20} />} disabled={changePassword.isPending}>
          {messages.app.saveChanges}
        </Button>
      </form>
    </Card>
  );
}
