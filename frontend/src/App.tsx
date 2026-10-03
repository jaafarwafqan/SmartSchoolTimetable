import {
  AlertCircle,
  ArrowRight,
  Check,
  Clipboard,
  GraduationCap,
  House,
  KeyRound,
  LogOut,
  Printer,
  RefreshCw,
  Save,
  Settings as SettingsIcon,
  ShieldCheck,
  UserRound,
} from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  BrowserRouter,
  Link,
  Route,
  Routes,
  useNavigate,
} from "react-router-dom";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { apiRequest, userErrorMessage } from "./api";
import { PasswordField } from "./components/PasswordField";
import { Button } from "./components/ui/button";
import { Card } from "./components/ui/card";
import { Input } from "./components/ui/input";
import { isLatinFree, translatedFieldError, ApiRequestError } from "./i18n/errors";
import { messages } from "./i18n/messages";
import { useSessionStore, type BootstrapState } from "./state/session";

type RecoveryResponse = { recoveryCode: string };
type SetupResponse = { recoveryCode: string };

function AlertMessage({ message }: { message: string | null }) {
  if (!message) return null;
  const safeMessage = isLatinFree(message) ? message : messages.errors.UNKNOWN_ERROR;
  return (
    <div className="alert-message" role="alert">
      <AlertCircle aria-hidden="true" size={20} strokeWidth={2} />
      <span>{safeMessage}</span>
    </div>
  );
}

function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <main className="page-shell">
      <Card className="auth-card" aria-labelledby="page-title">
        <div className="brand-mark" aria-hidden="true">
          <GraduationCap size={26} strokeWidth={2} />
        </div>
        <p className="eyebrow">{messages.app.tagline}</p>
        <h1 id="page-title">{messages.app.brand}</h1>
        {children}
      </Card>
      <footer>{messages.app.tagline}</footer>
    </main>
  );
}

function useErrorText() {
  const [error, setError] = useState<string | null>(null);
  const showError = (reason: unknown) => {
    if (reason instanceof ApiRequestError && reason.fields.length > 0) {
      const details = reason.fields
        .map((item) => translatedFieldError(item.field, item.code))
        .filter(isLatinFree);
      setError(details.length > 0 ? details.join("، ") : messages.errors.VALIDATION_FAILED);
      return;
    }
    setError(userErrorMessage(reason));
  };
  return { error, setError, showError };
}

function SetupScreen() {
  const queryClient = useQueryClient();
  const setRecoveryCode = useSessionStore((state) => state.setRecoveryCode);
  const { error, setError, showError } = useErrorText();
  const setup = useMutation({
    mutationFn: (payload: { username: string; password: string; confirmPassword: string }) =>
      apiRequest<SetupResponse>("/api/v1/auth/setup", "POST", payload),
    onSuccess: async (result) => {
      setRecoveryCode(result.recoveryCode);
      await queryClient.invalidateQueries({ queryKey: ["bootstrap"] });
    },
    onError: showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    const form = new FormData(event.currentTarget);
    const username = String(form.get("username") ?? "").trim();
    const password = String(form.get("password") ?? "");
    const confirmPassword = String(form.get("confirmPassword") ?? "");
    if (username.length < 3 || username.length > 64) {
      setError(messages.app.validationUsername);
      return;
    }
    if (password.length < 12) {
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
          <Input id="username" name="username" autoComplete="username" minLength={3} maxLength={64} required />
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

function LoginScreen({ onRecover }: { onRecover: () => void }) {
  const queryClient = useQueryClient();
  const { error, setError, showError } = useErrorText();
  const login = useMutation({
    mutationFn: (payload: { username: string; password: string }) =>
      apiRequest<void>("/api/v1/auth/login", "POST", payload),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ["bootstrap"] }),
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
        onClick={onRecover}
      >
        {messages.app.recoveryLink}
      </Button>
    </AuthLayout>
  );
}

function RecoveryScreen({
  code,
  onContinue,
}: {
  code: string;
  onContinue: () => Promise<void>;
}) {
  const [saved, setSaved] = useState(false);
  const { error, setError, showError } = useErrorText();
  const title = messages.app.codeTitle;

  async function copyCode() {
    try {
      await navigator.clipboard.writeText(code);
      setError(messages.app.codeCopied);
    } catch {
      setError(messages.app.codeCopyFailed);
    }
  }

  function saveCode() {
    const content = `${messages.app.codeTitle}\n${messages.app.codeWarning}\n\n${code}\n`;
    const file = new Blob([content], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(file);
    const link = document.createElement("a");
    link.href = url;
    link.download = "رمز-الاسترداد.txt";
    link.click();
    URL.revokeObjectURL(url);
    setError(messages.app.fileSaved);
  }

  async function continueToApp() {
    setError(null);
    try {
      await onContinue();
    } catch (reason) {
      showError(reason);
    }
  }

  return (
    <AuthLayout>
      <h2>{title}</h2>
      <p className="warning-text">{messages.app.codeWarning}</p>
      <output className="recovery-code" aria-label={messages.app.recoveryCode} dir="ltr">
        {code}
      </output>
      <AlertMessage message={error} />
      <div className="action-row">
        <Button type="button" variant="secondary" icon={<Clipboard aria-hidden="true" size={20} />} onClick={copyCode}>
          {messages.app.copyCode}
        </Button>
        <Button
          type="button"
          variant="secondary"
          icon={<Printer aria-hidden="true" size={20} />}
          onClick={() => window.print()}
        >
          {messages.app.printCode}
        </Button>
        <Button type="button" variant="secondary" icon={<Save aria-hidden="true" size={20} />} onClick={saveCode}>
          {messages.app.saveCode}
        </Button>
      </div>
      <p className="helper-text">{messages.app.codeStored}</p>
      <label className="checkbox-row">
        <input type="checkbox" checked={saved} onChange={(event) => setSaved(event.currentTarget.checked)} />
        <span>{messages.app.confirmCodeSaved}</span>
      </label>
      <Button
        type="button"
        icon={<Check aria-hidden="true" size={20} />}
        disabled={!saved}
        onClick={continueToApp}
      >
        {messages.app.continue}
      </Button>
      {!saved && <p className="helper-text">{messages.app.continueDisabled}</p>}
    </AuthLayout>
  );
}

function RecoveryForm({ onBack }: { onBack: () => void }) {
  const queryClient = useQueryClient();
  const setRecoveryCode = useSessionStore((state) => state.setRecoveryCode);
  const { error, setError, showError } = useErrorText();
  const recover = useMutation({
    mutationFn: (payload: { recoveryCode: string; newPassword: string }) =>
      apiRequest<RecoveryResponse>("/api/v1/auth/recovery", "POST", payload),
    onSuccess: async (result) => {
      setRecoveryCode(result.recoveryCode);
      await queryClient.invalidateQueries({ queryKey: ["bootstrap"] });
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
      <Button type="button" variant="ghost" icon={<ArrowRight aria-hidden="true" size={18} />} onClick={onBack}>
        {messages.app.backToLogin}
      </Button>
    </AuthLayout>
  );
}

function RecoveryGate({
  code,
  onAcknowledge,
}: {
  code: string;
  onAcknowledge: () => Promise<void>;
}) {
  return <RecoveryScreen code={code} onContinue={onAcknowledge} />;
}

function SettingsScreen({ bootstrap }: { bootstrap: BootstrapState }) {
  const queryClient = useQueryClient();
  const setRecoveryCode = useSessionStore((state) => state.setRecoveryCode);
  const { error, setError, showError } = useErrorText();
  const [passwordMessage, setPasswordMessage] = useState<string | null>(null);
  const regenerate = useMutation({
    mutationFn: (currentPassword: string) =>
      apiRequest<RecoveryResponse>("/api/v1/auth/recovery-code/regenerate", "POST", { currentPassword }),
    onSuccess: async (result) => {
      setRecoveryCode(result.recoveryCode);
      await queryClient.invalidateQueries({ queryKey: ["bootstrap"] });
    },
    onError: showError,
  });
  const changePassword = useMutation({
    mutationFn: (payload: { currentPassword: string; newPassword: string }) =>
      apiRequest<void>("/api/v1/auth/change-password", "POST", payload),
    onSuccess: async () => {
      setPasswordMessage(messages.app.passwordChanged);
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ["bootstrap"] });
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
      {bootstrap.recoveryCodeAcknowledgementRequired &&
        <p className="warning-text" role="status">{messages.app.recoveryPendingSettings}</p>}
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

function AppShell({ bootstrap }: { bootstrap: BootstrapState }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { error, setError, showError } = useErrorText();
  const logout = useMutation({
    mutationFn: () => apiRequest<void>("/api/v1/auth/logout", "POST", {}),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["bootstrap"] });
      navigate("/", { replace: true });
    },
    onError: showError,
  });

  useEffect(() => {
    if (bootstrap.inactivityTimeoutMinutes === null) return;
    let timeout: number;
    const reset = () => {
      window.clearTimeout(timeout);
      timeout = window.setTimeout(() => {
        void apiRequest<void>("/api/v1/auth/logout", "POST", {})
          .catch(() => undefined)
          .finally(() => {
            void queryClient.invalidateQueries({ queryKey: ["bootstrap"] });
            navigate("/", { replace: true });
          });
      }, bootstrap.inactivityTimeoutMinutes! * 60_000);
    };
    const events = ["pointerdown", "keydown", "touchstart"];
    events.forEach((event) => window.addEventListener(event, reset, { passive: true }));
    reset();
    return () => {
      window.clearTimeout(timeout);
      events.forEach((event) => window.removeEventListener(event, reset));
    };
  }, [bootstrap.inactivityTimeoutMinutes, navigate, queryClient]);

  return (
    <main className="app-shell">
      <header className="app-header">
        <Link className="brand-link" to="/" aria-label={messages.app.home}>
          <House aria-hidden="true" size={22} strokeWidth={2} />
          <span>{messages.app.brand}</span>
        </Link>
        <nav aria-label={messages.app.settings}>
          <Link className="nav-link" to="/settings">
            <SettingsIcon aria-hidden="true" size={20} strokeWidth={2} />
            <span>{messages.app.settings}</span>
          </Link>
          <Button
            variant="secondary"
            icon={<LogOut aria-hidden="true" size={20} />}
            disabled={logout.isPending}
            onClick={() => {
              setError(null);
              logout.mutate();
            }}
          >
            {messages.app.logout}
          </Button>
        </nav>
      </header>
      <AlertMessage message={error} />
      <Routes>
        <Route
          path="/"
          element={
            <section className="welcome-card">
              <div className="welcome-icon"><House aria-hidden="true" size={28} /></div>
              <h1>{messages.app.home}</h1>
              <p>{messages.app.signedInAs} {bootstrap.username}</p>
              <p className="helper-text">{messages.app.noSystemFeatures}</p>
              <Link className="nav-link settings-action" to="/settings">
                <SettingsIcon aria-hidden="true" size={20} />
                <span>{messages.app.settings}</span>
              </Link>
            </section>
          }
        />
        <Route path="/settings" element={<SettingsScreen bootstrap={bootstrap} />} />
        <Route
          path="*"
          element={
            <section className="welcome-card" role="alert">
              <AlertMessage message={messages.app.notFoundPage} />
              <Link className="nav-link" to="/">
                <House aria-hidden="true" size={20} />
                <span>{messages.app.returnHome}</span>
              </Link>
            </section>
          }
        />
      </Routes>
    </main>
  );
}

function Application() {
  const queryClient = useQueryClient();
  const bootstrap = useSessionStore((state) => state.bootstrap);
  const setBootstrap = useSessionStore((state) => state.setBootstrap);
  const recoveryCode = useSessionStore((state) => state.recoveryCode);
  const setRecoveryCode = useSessionStore((state) => state.setRecoveryCode);
  const [recovering, setRecovering] = useState(false);
  const [bootstrapError, setBootstrapError] = useState<string | null>(null);
  const bootstrapQuery = useQuery({
    queryKey: ["bootstrap"],
    queryFn: () => apiRequest<BootstrapState>("/api/v1/bootstrap"),
    retry: false,
  });

  useEffect(() => {
    if (bootstrapQuery.data) {
      setBootstrap(bootstrapQuery.data);
      setBootstrapError(null);
    } else if (bootstrapQuery.error) {
      setBootstrapError(userErrorMessage(bootstrapQuery.error));
    }
  }, [bootstrapQuery.data, bootstrapQuery.error, setBootstrap]);

  async function acknowledgeRecoveryCode() {
    await apiRequest<void>("/api/v1/auth/recovery-code/acknowledge", "POST", {});
    setRecoveryCode(null);
    await queryClient.invalidateQueries({ queryKey: ["bootstrap"] });
  }

  if (bootstrapQuery.isPending) {
    return <AuthLayout><p role="status">{messages.app.loading}</p></AuthLayout>;
  }
  if (bootstrapError) {
    return (
      <AuthLayout>
        <AlertMessage message={bootstrapError} />
        <Button
          type="button"
          icon={<RefreshCw aria-hidden="true" size={20} />}
          onClick={() => void bootstrapQuery.refetch()}
        >
          {messages.app.retry}
        </Button>
      </AuthLayout>
    );
  }
  const currentBootstrap = bootstrap ?? bootstrapQuery.data;
  if (!currentBootstrap) return <AuthLayout><AlertMessage message={messages.errors.UNKNOWN_ERROR} /></AuthLayout>;

  if (currentBootstrap.setupRequired) return <SetupScreen />;
  if (!currentBootstrap.authenticated) {
    return recovering
      ? <RecoveryForm onBack={() => setRecovering(false)} />
      : <LoginScreen onRecover={() => setRecovering(true)} />;
  }
  if (currentBootstrap.recoveryCodeAcknowledgementRequired) {
    if (recoveryCode) {
      return <RecoveryGate code={recoveryCode} onAcknowledge={acknowledgeRecoveryCode} />;
    }
  }

  return <AppShell bootstrap={currentBootstrap} />;
}

export default function App() {
  return (
    <BrowserRouter>
      <Application />
    </BrowserRouter>
  );
}
