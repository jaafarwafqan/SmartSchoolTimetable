# ADR 0003: Outbox + ChangeSeq sync model

> **Superseded — historical.** Kept for the record only; the product is single-user and local-only (see [ADR 0008](../../adr/0008-single-user-local-architecture.md)). Do not implement.


- Status: Superseded by ADR 0008
- Date: 2026-10-03

## Context
The original product scope called for disconnected client use with synchronization to a remote server.

## Historical decision
Use Dexie/IndexedDB on the client, outbox commands for queued writes, and a monotonic per-tenant `ChangeSeq` cursor on the server. Deletions are soft-deletes with tombstones retained for maximum offline age.

## Supersession
The owner clarified there is no remote service or network sync. The local SQLite database is authoritative and core operation works without internet. Do not implement remote outbox, tenant cursor, tombstone sync, or sync conflict protocols. See ADR 0008 and `OFFLINE_SYNC.md`.
