import { LogOut, RefreshCw } from "lucide-react";
import type { FormEvent } from "react";
import { AlertMessage } from "../../components/AlertMessage";
import { PasswordField } from "../../components/PasswordField";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { useErrorText } from "../../lib/useErrorText";
import { useLogout } from "./useLogout";
import { useRegenerateRecoveryCode } from "./useRecoveryCode";

/**
 * Blocking screen shown when the owner is signed in, the server reports an unacknowledged recovery code,
 * and the code is no longer in memory (for example after a reload). The application is not reachable
 * until a replacement code is generated with the current password and acknowledged.
 */
export function RecoveryPendingScreen() {
  const { error, setError, showError } = useErrorText();
  const regenerate = useRegenerateRecoveryCode(showError);
  const logout = useLogout(showError);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    const form = new FormData(event.currentTarget);
    regenerate.mutate(String(form.get("pendingCurrentPassword") ?? ""));
  }

  return (
    <AuthLayout>
      <h2>{messages.app.recoveryPendingTitle}</h2>
      <p className="warning-text">{messages.app.recoveryPendingDescription}</p>
      <AlertMessage message={error} />
      <form noValidate onSubmit={submit}>
        <PasswordField
          id="pendingCurrentPassword"
          label={messages.app.currentPassword}
          autoComplete="current-password"
        />
        <Button type="submit" icon={<RefreshCw aria-hidden="true" size={20} />} disabled={regenerate.isPending}>
          {messages.app.generateCode}
        </Button>
      </form>
      <Button
        type="button"
        variant="ghost"
        icon={<LogOut aria-hidden="true" size={18} />}
        disabled={logout.isPending}
        onClick={() => {
          setError(null);
          logout.mutate();
        }}
      >
        {messages.app.logout}
      </Button>
    </AuthLayout>
  );
}
