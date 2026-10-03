import { Check, Clipboard, Printer, Save } from "lucide-react";
import { useState } from "react";
import { AlertMessage } from "../../components/AlertMessage";
import { Button } from "../../components/ui/button";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { useErrorText } from "../../lib/useErrorText";

type RecoveryCodeScreenProps = {
  code: string;
  onContinue: () => Promise<void>;
};

export function RecoveryCodeScreen({ code, onContinue }: RecoveryCodeScreenProps) {
  const [saved, setSaved] = useState(false);
  const { error, setError, showError } = useErrorText();

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
    link.download = messages.app.recoveryCodeFileName;
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
      <h2>{messages.app.codeTitle}</h2>
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
