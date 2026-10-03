import { UserRound } from "lucide-react";
import { useMutation } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { apiRequest } from "../../api";
import { AlertMessage } from "../../components/AlertMessage";
import { PasswordField } from "../../components/PasswordField";
import { Button } from "../../components/ui/button";
import { Input } from "../../components/ui/input";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { passwordMinLength, usernameMaxLength, usernameMinLength } from "../../lib/credentialRules";
import { useErrorText } from "../../lib/useErrorText";
import { useStoreIssuedRecoveryCode, type RecoveryCodeResponse } from "./useRecoveryCode";

type SetupPayload = { username: string; password: string; confirmPassword: string };

export function SetupScreen() {
  const storeIssuedCode = useStoreIssuedRecoveryCode();
  const { error, setError, showError } = useErrorText();
  const setup = useMutation({
    mutationFn: (payload: SetupPayload) =>
      apiRequest<RecoveryCodeResponse>("/api/v1/auth/setup", "POST", payload),
    onSuccess: (result) => storeIssuedCode(result.recoveryCode),
    onError: showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    const form = new FormData(event.currentTarget);
    const username = String(form.get("username") ?? "").trim();
    const password = String(form.get("password") ?? "");
    const confirmPassword = String(form.get("confirmPassword") ?? "");
    if (username.length < usernameMinLength || username.length > usernameMaxLength) {
      setError(messages.app.validationUsername);
      return;
    }
    if (password.length < passwordMinLength) {
      setError(messages.app.validationPassword);
      return;
    }
    setup.mutate({ username, password, confirmPassword });
  }

  return (
    <AuthLayout>
      <h2>{messages.app.setupTitle}</h2>
      <p>{messages.app.setupDescription}</p>
      <AlertMessage message={error} />
      <form noValidate onSubmit={submit}>
        <div className="field">
          <label htmlFor="username">{messages.app.username}</label>
          <Input
            id="username"
            name="username"
            autoComplete="username"
            minLength={usernameMinLength}
            maxLength={usernameMaxLength}
            required
          />
        </div>
        <PasswordField id="password" label={messages.app.password} autoComplete="new-password" />
        <PasswordField id="confirmPassword" label={messages.app.confirmPassword} autoComplete="new-password" />
        <Button type="submit" icon={<UserRound aria-hidden="true" size={20} />} disabled={setup.isPending}>
          {messages.app.createAccount}
        </Button>
      </form>
    </AuthLayout>
  );
}
