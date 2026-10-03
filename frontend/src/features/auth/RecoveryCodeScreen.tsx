import { Check, Clipboard, ClipboardCheck, Printer, Save } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { useEffect, useState } from "react";
import { Button } from "../../components/ui/button";
import { Checkbox } from "../../components/ui/checkbox";
import { messages } from "../../i18n/messages";
import { AuthLayout } from "../../layout/AuthLayout";
import { useFormFeedback } from "../../lib/useFormFeedback";

type RecoveryCodeScreenProps = {
  code: string;
  onContinue: () => Promise<void>;
};

const copiedResetMs = 2_500;
/** Group separator of the recovery code value itself (not display text). */
const codeSeparator = "-";

export function RecoveryCodeScreen({ code, onContinue }: RecoveryCodeScreenProps) {
  const [saved, setSaved] = useState(false);
  const [copied, setCopied] = useState(false);
  const [continuing, setContinuing] = useState(false);
  const feedback = useFormFeedback();

  useEffect(() => {
    if (!copied) return;
    const timeout = window.setTimeout(() => setCopied(false), copiedResetMs);
    return () => window.clearTimeout(timeout);
  }, [copied]);

  async function copyCode() {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      feedback.showSuccess(messages.app.codeCopied);
    } catch {
      feedback.setError(messages.app.codeCopyFailed);
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
    feedback.showSuccess(messages.app.fileSaved);
  }

  async function continueToApp() {
    feedback.reset();
    setContinuing(true);
    try {
      await onContinue();
    } catch (reason) {
      feedback.showError(reason);
      setContinuing(false);
    }
  }

  return (
    <AuthLayout title={messages.app.codeTitle}>
      <Alert tone="warning" message={messages.app.codeWarning} />
      <output className="recovery-code" aria-label={messages.app.recoveryCode} dir="ltr">
        {code.split(codeSeparator).map((group, index) => (
          <span key={index} className="code-group">
            {index > 0 && <span className="code-separator">{codeSeparator}</span>}
            {group}
          </span>
        ))}
      </output>
      <div className="code-actions">
        <Button
          variant="secondary"
          icon={copied
            ? <ClipboardCheck aria-hidden="true" size={18} />
            : <Clipboard aria-hidden="true" size={18} />}
          onClick={copyCode}
        >
          {copied ? messages.app.copied : messages.app.copyCode}
        </Button>
        <Button variant="secondary" icon={<Printer aria-hidden="true" size={18} />} onClick={() => window.print()}>
          {messages.app.printCode}
        </Button>
        <Button variant="secondary" icon={<Save aria-hidden="true" size={18} />} onClick={saveCode}>
          {messages.app.saveCode}
        </Button>
      </div>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <p className="helper-text">{messages.app.codeStored}</p>
      <Checkbox checked={saved} onChange={(event) => setSaved(event.currentTarget.checked)}>
        {messages.app.confirmCodeSaved}
      </Checkbox>
      <Button
        block
        icon={<Check aria-hidden="true" size={18} />}
        disabled={!saved}
        loading={continuing}
        onClick={continueToApp}
      >
        {messages.app.continue}
      </Button>
      {!saved && <p className="helper-text helper-center">{messages.app.continueDisabled}</p>}
    </AuthLayout>
  );
}
