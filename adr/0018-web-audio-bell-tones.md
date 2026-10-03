# ADR 0018: Synthesized bell tone previews

- Status: Accepted for Phase 2B
- Date: 2026-10-03

## Context

Phase 2 configures bell preferences and offers a local preview. The app must not add audio files or licensing concerns, and actual scheduled ringing is explicitly deferred to Phase 7.

## Decision

Provide four built-in tone patterns using the browser Web Audio API. Only create audio after the owner presses the preview button; close the `AudioContext` when the short preview ends. Persist the selected tone and break bell flag. Per-lesson start/end flags remain on their period rows.

## Consequences

- No package dependency, external service, or audio asset is introduced.
- Browsers may require a user gesture or audio permission; the UI reports preview failure in Arabic.
- No timer or background process rings bells in Phase 2.

## Alternatives considered

- Bundled sound files: rejected to avoid assets and licensing review.
- Automatic ringing now: rejected because live ringing is Phase 7.
