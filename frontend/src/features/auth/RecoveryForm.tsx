import { ArrowRight, ShieldCheck } from "lucide-react";
import { useMutation } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { apiRequest } from "../../api";
import { AlertMessage } from "../../components/AlertMessage";
import { PasswordField } from "../../components/PasswordField";
import { Button } from "../../components/ui/button";
import { Input } from "../../components/ui/input";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { useErrorText } from "../../lib/useErrorText";
import { useUiStore } from "../../state/session";
import { useStoreIssuedRecoveryCode, type RecoveryCodeResponse } from "./useRecoveryCode";

export function RecoveryForm() {
  const storeIssuedCode = useStoreIssuedRecoveryCode();
  const setRecoveryFormOpen = useUiStore((state) => state.setRecoveryFormOpen);
  const { error, setError, showError } = useErrorText();
  const recover = useMutation({
    mutationFn: (payload: { recoveryCode: string; newPassword: string }) =>
      apiRequest<RecoveryCodeResponse>("/api/v1/auth/recovery", "POST", payload),
    onSuccess: async (result) => {
      await storeIssuedCode(result.recoveryCode);
      // Close the form only after the signed-in state has loaded, so a later logout shows the login screen.
      setRecoveryFormOpen(false);
    },
    onError: showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    const form = new FormData(event.currentTarget);
    const newPassword = String(form.get("newPassword") ?? "");
    if (newPassword !== String(form.get("confirmPassword") ?? "")) {
      setError(messages.app.validationConfirm);
      return;
    }
    recover.mutate({
      recoveryCode: String(form.get("recoveryCode") ?? "").trim(),
      newPassword,
    });
  }

  return (
    <AuthLayout>
      <h2>{messages.app.recoveryTitle}</h2>
      <p>{messages.app.codeWarning}</p>
      <AlertMessage message={error} />
      <form noValidate onSubmit={submit}>
        <div className="field">
          <label htmlFor="recoveryCode">{messages.app.recoveryCode}</label>
          <Input id="recoveryCode" name="recoveryCode" autoComplete="off" dir="ltr" required />
        </div>
        <PasswordField id="newPassword" label={messages.app.newPassword} autoComplete="new-password" />
        <PasswordField id="confirmPassword" label={messages.app.confirmPassword} autoComplete="new-password" />
        <Button type="submit" icon={<ShieldCheck aria-hidden="true" size={20} />} disabled={recover.isPending}>
          {messages.app.resetPassword}
        </Button>
      </form>
      <Button
        type="button"
        variant="ghost"
        icon={<ArrowRight aria-hidden="true" size={18} />}
        onClick={() => setRecoveryFormOpen(false)}
      >
        {messages.app.backToLogin}
      </Button>
    </AuthLayout>
  );
}
