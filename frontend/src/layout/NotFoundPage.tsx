import { House } from "lucide-react";
import { Link } from "react-router-dom";
import { AlertMessage } from "../components/AlertMessage";
import { messages } from "../i18n/messages";

export function NotFoundPage() {
  return (
    <section className="welcome-card">
      <AlertMessage message={messages.app.notFoundPage} />
      <Link className="nav-link" to="/">
        <House aria-hidden="true" size={20} />
        <span>{messages.app.returnHome}</span>
      </Link>
    </section>
  );
}
