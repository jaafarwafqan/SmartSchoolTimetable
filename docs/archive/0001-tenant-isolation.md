# ADR 0001: Multi-tenant school isolation

> **Superseded — historical.** Kept for the record only; the product is single-user and local-only (see [ADR 0008](../../adr/0008-single-user-local-architecture.md)). Do not implement.


- Status: Superseded by ADR 0008
- Date: 2026-10-03

## Context
The original platform scope called for many schools (tenants) in one deployment with tenant separation.

## Historical decision
Each tenant maps to exactly one school. Tenant identity originates only from a JWT `tenant_id` claim. Tenant-owned tables include `TenantId`, with EF global filters, a SaveChanges interceptor, and PostgreSQL RLS.

## Supersession
The owner clarified the product as one local installation, one owner account, and no network access. The multi-tenant/JWT/RLS model is therefore not part of the active design. See ADR 0008 for the local-only architecture. This historical ADR is retained to record the superseded decision; do not implement its tenant mechanisms.
