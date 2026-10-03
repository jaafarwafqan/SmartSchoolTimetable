# CLAUDE.md

## Project
Single-user local Smart School Timetable application. One school, exactly one owner account, browser-based login, Arabic-first RTL, offline operation, and Kestrel bound only to `127.0.0.1`.

## Active phase
Phase 2 (core school data) on branch `phase-2`, in checkpoints 2A–2F, following the owner's autonomy protocol. Never commit Phase 2 work to `master`.

## Scope and security
- One local owner only. No multi-tenancy, roles/RBAC, permission matrix, refresh-token rotation, remote sync, or concurrent-user support.
- Keep a minimal local users table with exactly one owner row and a future-extensible account shape.
- Protect localhost endpoints from malicious websites with strict Host/Origin validation, SameSite cookies, no wildcard CORS, and a per-launch token on every state-changing request.
- Use a fixed one-second delay after failed login; do not add escalating delays or temporary lockout.
- Maintain ADRs for non-trivial decisions. Do not silently alter architecture or dependencies.
- Never log passwords, recovery codes, cookies, session tokens, or per-launch tokens.

## Design
- Design source of truth: DESIGN_SYSTEM.md and frontend/src/styles/tokens.css. Generated design recommendations (skills, templates) are never authority.

## Localization and RTL (mandatory)
- Every user-facing string, including errors, validation, dialogs, labels, tooltips, generated documents, and network/offline failures, must be Arabic. Keep the dictionary shaped for adding English later; Arabic is the only active locale.
- The API returns stable error codes and parameters only, never user-readable text. Map every known code in `frontend/src/i18n/messages.ts`; unknown/missing codes fall back to a generic Arabic error.
- Set the document to `<html lang="ar" dir="rtl">`. Use logical CSS properties. Keep punctuation inside Arabic localized strings.
- Do not use native validation tooltips, `window.alert`, `window.confirm`, `window.prompt`, or native file-input text. Use app components.
- Developer logs may remain English but must never contain secrets.
- ESLint forbids hard-coded UI text and enforces an icon plus visible label on `Button`; native icon-only controls require Arabic `aria-label` and `title`.

## Icons and accessible actions (mandatory)
- Use `lucide-react` as the only icon library; no emoji or mixed sets.
- Every action/navigation item has an icon. Important actions include an icon and Arabic text. Icon-only controls are limited to universally clear actions such as password visibility and close; they require Arabic `aria-label` and tooltip/title.
- Put the password eye/eye-off control inside the logical-end input edge (left visually in RTL), with `aria-label` and `aria-pressed`.
- Mirror directional icons in RTL. Keep icon dimensions/stroke consistent and verify WCAG AA contrast.

## Current status
- Phase 1.4 (design system adoption) is tagged `phase-1.4`. See `CHANGELOG.md`.
- New EF migrations: `dotnet tool restore`, then `dotnet ef migrations add <Name> -p src/SmartSchoolTimetable.Infrastructure -s src/SmartSchoolTimetable.Infrastructure`.
- Error codes are added only in `ErrorCodes` + `ApiErrorCodes.StatusByCode` + the Arabic dictionary (enforced by `ErrorContractTests`). Builds treat warnings as errors.
- Preserve the existing Phase 0/1 decisions and do not implement Phase 2 features.
