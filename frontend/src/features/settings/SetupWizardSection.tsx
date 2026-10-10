import { Wand2 } from "lucide-react";
import { Link } from "react-router-dom";
import { messages } from "../../i18n/messages";
import { SettingsSection } from "./SettingsSection";

const text = messages.school.wizard;

/** Settings entry to the setup wizard (spec 2.5 §5: reachable from the dashboard and Settings). */
export function SetupWizardSection() {
  return (
    <SettingsSection id="setup-wizard" icon={Wand2} title={text.settingsTitle} description={text.settingsDescription}>
      <Link className="ui-button ui-button-secondary ui-button-md" to="/setup">
        <Wand2 aria-hidden="true" size={20} />
        <span>{messages.school.nav.setupWizard}</span>
      </Link>
    </SettingsSection>
  );
}
