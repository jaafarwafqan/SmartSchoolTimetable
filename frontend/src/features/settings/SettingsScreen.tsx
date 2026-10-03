import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import type { Bootstrap } from "../../lib/bootstrapQuery";
import { InactivitySection } from "./InactivitySection";
import { PasswordSection } from "./PasswordSection";
import { RecoveryCodeSection } from "./RecoveryCodeSection";

export function SettingsScreen({ bootstrap }: { bootstrap: Bootstrap }) {
  return (
    <div className="page">
      <PageHeader title={messages.app.settings} description={messages.app.settingsDescription} />
      <div className="settings-grid">
        <InactivitySection bootstrap={bootstrap} />
        <RecoveryCodeSection />
        <PasswordSection />
      </div>
    </div>
  );
}
