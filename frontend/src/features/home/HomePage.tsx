import { CalendarClock, ChevronLeft, ShieldCheck } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { Link } from "react-router-dom";
import { messages } from "../../i18n/messages";

export function HomePage({ username }: { username: string | null }) {
  return (
    <div className="page">
      <header className="page-header">
        <p className="page-eyebrow">{messages.app.home}</p>
        <h1>{messages.app.greeting(username ?? "")}</h1>
        <p>{messages.app.homeDescription}</p>
      </header>

      <section className="upcoming-panel" aria-labelledby="upcoming-title">
        <span className="icon-badge" aria-hidden="true">
          <CalendarClock size={22} strokeWidth={2} />
        </span>
        <div>
          <h2 id="upcoming-title">{messages.app.upcomingTitle}</h2>
          <Alert tone="info" message={messages.app.noSystemFeatures} />
        </div>
      </section>

      <section aria-labelledby="quick-actions-title">
        <h2 id="quick-actions-title" className="section-title">{messages.app.quickActions}</h2>
        <div className="action-grid">
          <Link className="action-card" to="/settings">
            <span className="icon-badge" aria-hidden="true">
              <ShieldCheck size={22} strokeWidth={2} />
            </span>
            <span className="action-card-text">
              <strong>{messages.app.accountSettingsTitle}</strong>
              <span>{messages.app.accountSettingsDescription}</span>
            </span>
            <ChevronLeft className="action-card-arrow" aria-hidden="true" size={20} />
          </Link>
        </div>
      </section>
    </div>
  );
}
