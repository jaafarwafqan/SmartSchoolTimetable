# Security

## Tenant isolation
- Tenant resolved only from JWT `tenant_id` claim.
- No tenant id accepted from request body, query string, or client state.
- EF global filters, SaveChanges interceptor, and PostgreSQL RLS are required.

## Identity
- Access tokens valid for 15 minutes.
- Refresh tokens rotate and detect reuse.
- Refresh tokens are stored securely and are revocable.
- Password hashing uses adaptive hashing; invalid attempts are rate-limited with lockout protections.

## Audit
Append-only auditing records tenant, user, action, entity, entity id, before/after JSON, IP, correlation id, and reason. This includes login, security events, generation, approval, publication, rollback, role changes, and tenant-operations.

## Teacher personal data
Teacher personal information is retained only as needed for scheduling and compliance. Deletion and retention policy will be finalized during implementation and documented in a formal data retention schedule.

## Local device risk
IndexedDB is not encrypted. The platform stores the minimum locally required data and wipes cached tenant state on logout or expiry of the offline session.

## Security controls to implement in Phase 1+
- CORS hardening
- security headers
- rate limiting
- CSRF handling where applicable
- secret management via environment or secret manager
- structured, secret-safe logging
