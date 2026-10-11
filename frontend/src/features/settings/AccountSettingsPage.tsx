import { ShieldCheck } from "lucide-react";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import type { Bootstrap } from "../../lib/bootstrapQuery";
import { InactivitySection } from "./InactivitySection";
import { PasswordSection } from "./PasswordSection";
import { RecoveryCodeSection } from "./RecoveryCodeSection";
import { UsernameSection } from "./UsernameSection";

/** «الحساب والأمان»: username, password, recovery code and the automatic lock: everything about the owner account. */
export function AccountSettingsPage({ bootstrap }: { bootstrap: Bootstrap }) {
  return (
    <div className="page page-full">
      <PageHeader icon={ShieldCheck} title={messages.school.nav.settingsAccount} description={messages.app.accountDescription} />
      <div className="settings-grid">
        <UsernameSection username={bootstrap.username} />
        <PasswordSection />
        <RecoveryCodeSection />
        <InactivitySection bootstrap={bootstrap} />
      </div>
    </div>
  );
}
