# Observability and Operations

## Logging
Structured logs via Serilog with correlation id, request id, tenant id, user id, generation id, and operation metadata. Sensitive values must never be logged.

## Health checks
- `/health/live`
- `/health/ready`

## Metrics
- API request latency
- queue depth and job latency
- solver completion time and status
- sync success/failure counts
- conflict volume and retry rates

## Operational practices
- Dockerized services for api, worker, web
- CI pipeline verifies restore, build, lint, unit tests, integration tests, E2E tests, and container builds
- Deployment should include release checks for migrations and tenant-specific configuration
