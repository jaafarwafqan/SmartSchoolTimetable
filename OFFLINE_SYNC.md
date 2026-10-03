# Offline Sync Design

## Overview
The server remains the source of truth. The frontend keeps a tenant-scoped, local replica using IndexedDB (Dexie) and an outbox-based sync model.

## Sync rules
- Each mutation is recorded as an outbox command with `clientId`, `createdAt`, `baseVersion`, command type, payload.
- Commands are idempotent by `clientId`.
- Reconnect flow: push queued changes, then pull server changes using `ChangeSeq`.
- The cursor is monotonic per tenant and never a timestamp.
- Deletions are soft deletes with tombstones retained for the maximum offline age.

## Conflict rules
- Attendance and daily monitoring use last-writer-wins per `Teacher + Date + Lesson` with audit trail.
- Timetable edits reject stale `baseVersion` with `409 Conflict`.
- Published or archived timetable edits are rejected and surfaced to the UI.

## Offline UX
- Show online/offline, pending changes, last sync time, sync status, retry action.
- Retry with exponential backoff.
- Never silently discard data or conflict resolution decisions.
- Offline timetable printing uses `window.print()` with dedicated RTL-aware A4 print CSS. It does not use a client-side PDF library; full QuestPDF export is online-only.

## Data retention
- Local data is deleted on logout or after offline-session expiry.
- Minimum necessary data is retained; tenant data is wiped on expiration.
