# ADR 0014: Bundled Noto Sans Arabic UI font

- Status: Accepted (font named by the owner in DESIGN_SYSTEM.md, Phase 1.4)
- Date: 2026-10-03
- Supersedes: an uncommitted Phase 1.3 UI-polish proposal to bundle Cairo (never released)

## Context
The UI used `Tahoma, Arial`, which the owner rejected as dated, and which renders differently between machines. The app must work offline and send no external requests (ADR 0008; CSP `default-src 'self'`), so a web-font CDN is not an option. DESIGN_SYSTEM.md section 3 names Noto Sans Arabic as the UI font and Noto Naskh Arabic for print/PDF.

## Decision
Add `@fontsource-variable/noto-sans-arabic` **5.3.0** (pinned exactly) and import it once in `frontend/src/main.tsx`. Vite copies the `woff2` files into `wwwroot/assets`, and they are served from `127.0.0.1`. The family name is referenced only in `frontend/src/styles/tokens.css` (`--font-sans`). The Cairo package installed during the Phase 1.3 polish was removed.

- **Purpose:** one consistent, legible Arabic/Latin UI typeface on every machine, offline.
- **Security:**
  - Only CSS and font files; no JavaScript runs.
  - No runtime network access; compatible with the CSP.
  - `npm audit` reports 0 vulnerabilities.
- **Licence:** SIL Open Font License 1.1.
- **Maintenance:** published by the Fontsource project (actively maintained; updated 2026-07). The exact version pin avoids silent changes.
- **Size:** browsers download only the subsets whose characters are used (Arabic, Latin, Latin-extended, math, symbols).
- **Alternatives considered:**
  - Tahoma/Segoe UI system fonts: rejected by the owner, and inconsistent across machines.
  - Google Fonts CDN: breaks offline use and the CSP.
  - Cairo: proposed earlier; superseded by the owner's design system.
  - Copying a TTF by hand: no versioned update path.

## Consequences
- The fallback stack `"Noto Sans Arabic Variable", Tahoma, "Segoe UI", sans-serif` keeps the UI readable if the font fails to load.
- Changing the font is a token change in `tokens.css` plus one import, under the DESIGN_SYSTEM.md change policy.
