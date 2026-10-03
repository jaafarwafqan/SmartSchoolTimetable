import { GraduationCap } from "lucide-react";
import type { ReactNode } from "react";
import { Card } from "../components/ui/card";
import { messages } from "../i18n/messages";

type AuthLayoutProps = {
  title?: string;
  description?: string;
  children: ReactNode;
};

export function AuthLayout({ title, description, children }: AuthLayoutProps) {
  return (
    <main className="auth-page">
      <Card className="auth-card" aria-labelledby={title ? "auth-title" : "auth-brand"}>
        <header className="auth-brand">
          <span className="brand-logo" aria-hidden="true">
            <GraduationCap size={24} strokeWidth={2} />
          </span>
          <span className="auth-brand-text">
            <h1 id="auth-brand">{messages.app.brand}</h1>
            <span>{messages.app.tagline}</span>
          </span>
        </header>
        {title && (
          <div className="auth-heading">
            <h2 id="auth-title">{title}</h2>
            {description && <p>{description}</p>}
          </div>
        )}
        {children}
      </Card>
    </main>
  );
}
