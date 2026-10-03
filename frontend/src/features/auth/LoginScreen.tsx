import { KeyRound, RefreshCw } from "lucide-react";
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
import { useRefreshBootstrap } from "./useBootstrap";

export function LoginScreen() {
  const refreshBootstrap = useRefreshBootstrap();
  const setRecoveryFormOpen = useUiStore((state) => state.setRecoveryFormOpen);
  const { error, setError, showError } = useErrorText();
  const login = useMutation({
    mutationFn: (payload: { username: string; password: string }) =>
      apiRequest<void>("/api/v1/auth/login", "POST", payload),
    onSuccess: () => refreshBootstrap(),
    onError: showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    const form = new FormData(event.currentTarget);
    login.mutate({
      username: String(form.get("username") ?? "").trim(),
      password: String(form.get("password") ?? ""),
    });
  }

  return (
    <AuthLayout>
      <h2>{messages.app.loginTitle}</h2>
      <p>{messages.app.loginDescription}</p>
      <AlertMessage message={error} />
      <form noValidate onSubmit={submit}>
        <div className="field">
          <label htmlFor="username">{messages.app.username}</label>
          <Input id="username" name="username" autoComplete="username" required />
        </div>
        <PasswordField id="password" label={messages.app.password} autoComplete="current-password" />
        <Button type="submit" icon={<KeyRound aria-hidden="true" size={20} />} disabled={login.isPending}>
          {messages.app.loginAction}
        </Button>
      </form>
      <Button
        type="button"
        variant="ghost"
        icon={<RefreshCw aria-hidden="true" size={18} />}
        onClick={() => setRecoveryFormOpen(true)}
      >
        {messages.app.recoveryLink}
      </Button>
    </AuthLayout>
  );
}
