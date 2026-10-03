import { LoaderCircle, RefreshCw } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { userErrorMessage } from "../../api";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { AppShell } from "../../layout/AppShell";
import { AuthLayout } from "../../layout/AuthLayout";
import { useUiStore } from "../../state/session";
import { LoginScreen } from "./LoginScreen";
import { RecoveryCodeScreen } from "./RecoveryCodeScreen";
import { RecoveryForm } from "./RecoveryForm";
import { RecoveryPendingScreen } from "./RecoveryPendingScreen";
import { SetupScreen } from "./SetupScreen";
import { useBootstrap } from "./useBootstrap";
import { useAcknowledgeRecoveryCode } from "./useRecoveryCode";

/** Chooses the screen from the server bootstrap state; protected routes render only inside AppShell. */
export function AppGate() {
  const bootstrapQuery = useBootstrap();
  const recoveryCode = useUiStore((state) => state.recoveryCode);
  const recoveryFormOpen = useUiStore((state) => state.recoveryFormOpen);
  const acknowledgeRecoveryCode = useAcknowledgeRecoveryCode();

  if (bootstrapQuery.isPending) {
    return (
      <AuthLayout>
        <p className="loading-state" role="status">
          <LoaderCircle className="ui-spin" aria-hidden="true" size={22} />
          <span>{messages.app.loading}</span>
        </p>
      </AuthLayout>
    );
  }

  const bootstrap = bootstrapQuery.data;
  if (!bootstrap) {
    return (
      <AuthLayout>
        <Alert tone="error" message={userErrorMessage(bootstrapQuery.error)} />
        <Button
          block
          icon={<RefreshCw aria-hidden="true" size={18} />}
          loading={bootstrapQuery.isFetching}
          onClick={() => void bootstrapQuery.refetch()}
        >
          {messages.app.retry}
        </Button>
      </AuthLayout>
    );
  }

  if (bootstrap.setupRequired) return <SetupScreen />;
  if (!bootstrap.authenticated) return recoveryFormOpen ? <RecoveryForm /> : <LoginScreen />;
  if (bootstrap.recoveryCodeAcknowledgementRequired) {
    return recoveryCode
      ? <RecoveryCodeScreen code={recoveryCode} onContinue={acknowledgeRecoveryCode} />
      : <RecoveryPendingScreen />;
  }

  return <AppShell bootstrap={bootstrap} />;
}
