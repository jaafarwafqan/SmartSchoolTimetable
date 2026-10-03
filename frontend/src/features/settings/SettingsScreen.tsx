import { messages } from "../../i18n/messages";
import type { Bootstrap } from "../../lib/bootstrapQuery";
import { InactivitySection } from "./InactivitySection";
import { PasswordSection } from "./PasswordSection";
import { RecoveryCodeSection } from "./RecoveryCodeSection";

export function SettingsScreen({ bootstrap }: { bootstrap: Bootstrap }) {
  return (
    <div className="page">
      <header className="page-header">
        <h1>{messages.app.settings}</h1>
        <p>{messages.app.settingsDescription}</p>
      </header>
      <div className="settings-grid">
        <InactivitySection bootstrap={bootstrap} />
        <RecoveryCodeSection />
        <PasswordSection />
      </div>
    </div>
  );
}
