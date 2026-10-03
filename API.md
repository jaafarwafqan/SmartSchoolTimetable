# API Design

## Principles
- OpenAPI-first contract
- versioned routes `/api/v1/...`
- thin endpoints delegating to IMediator
- no direct DbContext or repository usage in controllers
- consistent error envelope and authorization checks

## Key resource families
- Auth: login, refresh, revoke, logout
- School: profile and settings
- Teachers: CRUD, availability, constraints
- Scheduling: profile, validation, generation, diagnostics
- Timetables: draft/generate/approve/publish/rollback
- Sync: pull/push, conflict resolution
- Reporting: PDF and Excel exports

## Error model
- 400 validation
- 401 unauthenticated
- 403 forbidden
- 404 not found
- 409 conflict/version mismatch
- 429 rate limited
- 500 server error

## Observability requirements
- request correlation id
- user id and tenant id in every request log
- generation id for solver execution
