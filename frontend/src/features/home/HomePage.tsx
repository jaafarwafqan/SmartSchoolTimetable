import { House, Settings as SettingsIcon } from "lucide-react";
import { Link } from "react-router-dom";
import { messages } from "../../i18n/messages";

export function HomePage({ username }: { username: string | null }) {
  return (
    <section className="welcome-card">
      <div className="welcome-icon"><House aria-hidden="true" size={28} /></div>
      <h1>{messages.app.home}</h1>
      <p>{messages.app.signedInAs} {username}</p>
      <p className="helper-text">{messages.app.noSystemFeatures}</p>
      <Link className="nav-link settings-action" to="/settings">
        <SettingsIcon aria-hidden="true" size={20} />
        <span>{messages.app.settings}</span>
      </Link>
    </section>
  );
}
