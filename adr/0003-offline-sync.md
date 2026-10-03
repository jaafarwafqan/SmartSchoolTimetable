# ADR 0003: Outbox + ChangeSeq sync model

- Status: Accepted
- Date: 2026-10-03

## Context
The system must work offline for common timetable editing tasks while preserving server authority and safe reconciliation.

## Decision
Use Dexie/IndexedDB on the client, outbox commands for queued writes, and a monotonic per-tenant `ChangeSeq` cursor on the server. Deletions are soft-deletes with tombstones retained for maximum offline age.

## Consequences
- Good offline usability for draft edits and attendance.
- Clear conflict resolution strategy for stale clients.
- Requires careful retention and compaction planning.
- Server re-validation remains mandatory after sync.
