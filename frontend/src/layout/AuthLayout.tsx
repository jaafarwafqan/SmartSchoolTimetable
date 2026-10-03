import { GraduationCap } from "lucide-react";
import type { ReactNode } from "react";
import { Card } from "../components/ui/card";
import { messages } from "../i18n/messages";

export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <main className="page-shell">
      <Card className="auth-card" aria-labelledby="page-title">
        <div className="brand-mark" aria-hidden="true">
          <GraduationCap size={26} strokeWidth={2} />
        </div>
        <p className="eyebrow">{messages.app.tagline}</p>
        <h1 id="page-title">{messages.app.brand}</h1>
        {children}
      </Card>
      <footer>{messages.app.tagline}</footer>
    </main>
  );
}
