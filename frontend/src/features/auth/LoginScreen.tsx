import { KeyRound, LifeBuoy } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { useMutation } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { apiRequest } from "../../api";
import { PasswordField } from "../../components/PasswordField";
import { TextField } from "../../components/TextField";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { useFormFeedback, type FieldErrors } from "../../lib/useFormFeedback";
import { useUiStore } from "../../state/session";
import { useRefreshBootstrap } from "./useBootstrap";

export function LoginScreen() {
  const refreshBootstrap = useRefreshBootstrap();
  const setRecoveryFormOpen = useUiStore((state) => state.setRecoveryFormOpen);
  const loginNotice = useUiStore((state) => state.loginNotice);
  const setLoginNotice = useUiStore((state) => state.setLoginNotice);
  const feedback = useFormFeedback();
  const login = useMutation({
    mutationFn: (payload: { username: string; password: string }) =>
      apiRequest<void>("/api/v1/auth/login", "POST", payload),
    onSuccess: () => refreshBootstrap(),
    onError: feedback.showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    setLoginNotice(null);
    const form = new FormData(event.currentTarget);
    const username = String(form.get("username") ?? "").trim();
    const password = String(form.get("password") ?? "");
    const errors: FieldErrors = {};
    if (!username) errors.Username = messages.errors.REQUIRED;
    if (!password) errors.Password = messages.errors.REQUIRED;
    if (Object.keys(errors).length > 0) {
      feedback.showFieldErrors(errors);
      return;
    }
    login.mutate({ username, password });
  }

  return (
    <AuthLayout title={messages.app.loginTitle} description={messages.app.loginDescription}>
      <Alert tone="success" message={loginNotice} />
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <TextField
          id="username"
          label={messages.app.username}
          autoComplete="username"
          required
          field="Username"
          errors={feedback.fieldErrors}
        />
        <PasswordField
          id="password"
          label={messages.app.password}
          autoComplete="current-password"
          field="Password"
          errors={feedback.fieldErrors}
        />
        <Button type="submit" block icon={<KeyRound aria-hidden="true" size={18} />} loading={login.isPending}>
          {messages.app.loginAction}
        </Button>
      </form>
      <div className="auth-footer-actions">
        <Button
          variant="ghost"
          icon={<LifeBuoy aria-hidden="true" size={18} />}
          onClick={() => {
            setLoginNotice(null);
            setRecoveryFormOpen(true);
          }}
        >
          {messages.app.recoveryLink}
        </Button>
      </div>
    </AuthLayout>
  );
}
