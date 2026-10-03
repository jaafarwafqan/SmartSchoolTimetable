# ADR 0012: React owner UI and Arabic error contract

- Status: Accepted for Phase 1.2 by owner UX decision
- Date: 2026-10-03

## Context
The browser-based owner setup/login flow must support a one-time recovery-code acknowledgment flow, RTL accessibility, consistent icons, and reliable Arabic for every user-visible failure. Default framework error prose, browser-native validation, and English browser-locale messages must not leak into the UI.

## Decision
- Use React 19, strict TypeScript, Vite, Tailwind CSS, shadcn-style components, TanStack Query, Zustand, React Router, and lucide-react. The API serves the production Vite output from its `wwwroot`; no separate frontend server is required at runtime.
- Use lucide-react as the only icon set. Shared action buttons require an icon and a visible Arabic label. Password visibility and similarly universal actions may be icon-only only with Arabic `aria-label` and `title`.
- Arabic is the sole active locale. Keep the UI dictionary structured for later English support. All user-visible text, validation, errors, offline messages, and generated document labels are Arabic; RTL punctuation belongs inside the Arabic strings.
- API failures use a stable `{ code, correlationId, errors }` envelope. The API never returns display prose. Model-binding/validation/framework/security/database failures and unhandled exceptions must not expose ASP.NET ProblemDetails title/detail text. FluentValidation returns field/code pairs only.
- The UI maps every API code to Arabic and uses a generic Arabic fallback for missing/unknown codes and network failures. The document uses `lang="ar"` and `dir="rtl"`, forms use `noValidate`, and native `alert`/`confirm`/`prompt` UI is prohibited.
- ESLint rejects literal UI text and enforces the shared button icon/label contract; backend tests enumerate API error codes against the UI dictionary; Vitest and Playwright verify localization and principal error paths.

## Consequences
- UI code and localization are testable independently of the ASP.NET Core API while preserving the same-origin runtime boundary.
- Node.js is needed for frontend build/test; the .NET project builds the React bundle before producing the API.
- Every API error-code change must update the dictionary and the coverage test. New screens must pass lint, accessibility, RTL, and browser-language review.
