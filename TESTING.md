# Testing Strategy

## Test layers
- Unit tests for domain rules and validation
- Integration tests for PostgreSQL, Redis, auth, authorization, and tenancy
- E2E tests with Playwright for critical flows
- Architecture tests with NetArchTest
- Property-based tests with FsCheck
- Shared constraint test vectors for frontend/backend parity

## Required coverage
- Domain + scheduling logic: 90%+
- Critical flows: login, refresh, navigation, generation, approval, publish, rollback, sync, conflicts, exports

## Isolation tests
- Attempt cross-tenant access through API, EF, RLS, SignalR, Redis, and job execution
- Seed Tenant A and B and verify all cross-tenant operations are rejected

## Phase 0 validation
This phase does not implement production code; it validates the architecture and risk profile with spike outputs.
