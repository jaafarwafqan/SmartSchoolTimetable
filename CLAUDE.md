# CLAUDE.md

## Project
Single-user local Smart School Timetable application. One school, exactly one owner account, browser-based login, Arabic-first RTL, offline operation, and Kestrel bound only to `127.0.0.1`.

## Active phase
Phases 0–4 and the follow-up R1–R3 are delivered and merged to `master` (latest tag `phase-4i`). New work goes on a `work/<name>` branch, following the owner's autonomy protocol; never commit it to `master`. The owner merges after acceptance.

## Naming (mandatory)
- Branches are `work/<name>`. Tags are `phase-N[x]`. A branch and a tag never share a name (a shared name makes `git checkout`/`git push` ambiguous).

## Scope and security
- One local owner only. No multi-tenancy, roles/RBAC, permission matrix, refresh-token rotation, remote sync, or concurrent-user support.
- Keep a minimal local users table with exactly one owner row and a future-extensible account shape.
- Protect localhost endpoints from malicious websites with strict Host/Origin validation, SameSite cookies, no wildcard CORS, and a per-launch token on every state-changing request.
- Use a fixed one-second delay after failed login; do not add escalating delays or temporary lockout.
- Maintain ADRs for non-trivial decisions. Do not silently alter architecture or dependencies.
- Never log passwords, recovery codes, cookies, session tokens, or per-launch tokens.

## File deletion and destructive commands (owner rule, mandatory)
- Never delete anything outside the repository or the temporary folders created by the test suites themselves.
- Never delete backups.
- Any destructive command (delete, move, overwrite, reset, `Remove-Item`, `rm`, `git clean`, `git reset --hard`, database resets) requires the owner's explicit instruction naming the exact path. First list every path it would affect, then wait for the owner's confirmation. A path may be passed to `Remove-Item` (or any delete command) only after that confirmation.
- No default accounts, passwords or credentials in the source or configuration. An empty database must show the first-run setup screen (create owner, recovery code, then the wizard). Enforced by `FirstRunTests`.

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
- Delivered through Phase 4 and R1–R3 (tag `phase-4i`). See `CHANGELOG.md` and `docs/PHASE4G_REPORT.md`.
- New EF migrations: `dotnet tool restore`, then `dotnet ef migrations add <Name> -p src/SmartSchoolTimetable.Infrastructure -s src/SmartSchoolTimetable.Infrastructure`.
- Error codes are added only in `ErrorCodes` + `ApiErrorCodes.StatusByCode` + the Arabic dictionary (enforced by `ErrorContractTests`). Builds treat warnings as errors.
- Preserve the existing decisions (ADRs, `docs/DECISIONS_PENDING.md` for open items and `docs/DECISIONS_LOG.md` for closed ones); do not change approved behaviour silently.
