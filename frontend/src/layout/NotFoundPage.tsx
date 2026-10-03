import { House } from "lucide-react";
import { Alert } from "../components/ui/alert";
import { Link } from "react-router-dom";
import { messages } from "../i18n/messages";

export function NotFoundPage() {
  return (
    <div className="page page-narrow">
      <Alert tone="error" message={messages.app.notFoundPage} />
      <Link className="link-button" to="/">
        <House aria-hidden="true" size={18} />
        <span>{messages.app.returnHome}</span>
      </Link>
    </div>
  );
}
