import { Settings } from "lucide-react";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { PreferencesSection } from "./PreferencesSection";
import { SetupWizardSection } from "./SetupWizardSection";

/** «الإعدادات › عام»: display and print preferences and the setup wizard. Nothing about security or backups lives here. */
export function SettingsScreen() {
  return (
    <div className="page page-full">
      <PageHeader icon={Settings} title={messages.app.settings} description={messages.app.settingsDescription} />
      <div className="settings-grid">
        <PreferencesSection />
        <SetupWizardSection />
      </div>
    </div>
  );
}
