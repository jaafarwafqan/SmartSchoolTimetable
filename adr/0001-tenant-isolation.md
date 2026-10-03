# ADR 0001: Multi-tenant school isolation

- Status: Accepted
- Date: 2026-10-03

## Context
The platform must support many schools (tenants) in one deployment while guaranteeing tenant separation and compliant school-level data access.

## Decision
Each tenant maps to exactly one school. Tenant identity originates only from the JWT `tenant_id` claim and never from client-provided data. Every tenant-owned table includes `TenantId` and the application enforces EF global filters, a SaveChanges interceptor, and PostgreSQL RLS.

## Consequences
- Simpler and safer school model.
- Stronger security boundary between schools.
- Cross-tenant access attempts fail at app, data, and infrastructure layers.
- Additional complexity in audit and background job context restoration.
