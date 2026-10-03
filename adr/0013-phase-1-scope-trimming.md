# ADR 0013: Phase 1 scope trimming of enterprise-stack items

- Status: Accepted by the owner (Phase 1.4 instruction, 2026-10-03)
- Date: 2026-10-03

## Context
`MASTER_EXECUTION_PROMPT_v2` lists an enterprise stack: MediatR/CQRS endpoints, Serilog, `/health/live` and `/health/ready`, GitHub Actions CI, and an OpenAPI contract with a generated frontend client. ADR 0008 narrowed the product to a single-user, loopback-only local application but did not say which of these items still apply. The Phase 0–1.2 audit (`docs/AUDIT_REPORT.md` §11.3) found they were not implemented and not covered by any ADR, while `ARCHITECTURE.md` still claimed endpoints dispatch through `IMediator`. This ADR records the decision explicitly instead of leaving the gap silent.

## Decision
1. **MediatR/CQRS is not adopted.** Endpoints call Application services (`ILocalAuthService`) directly.
   - Endpoints stay thin: bind, validate with FluentValidation, call one Application method, map the result through the central error-code table.
   - Endpoints never reference `DbContext`, repositories or Domain entities. `ArchitectureTests` enforces this: the Api assembly must not reference the Domain or EF Core assemblies, and no Api type may use a `DbContext` or Domain type.
   - Reason: one user and a handful of use cases. A mediator adds a dependency and indirection without isolation, pipeline or scaling benefit here. It can be introduced later by a new ADR if cross-cutting handler behaviour is needed.
2. **Serilog is dropped for now.** The built-in `Microsoft.Extensions.Logging` console provider is used, with source-generated `LoggerMessage` methods (`LocalLog`).
   - Reason: there is no log shipping or remote collector (ADR 0008), so structured sinks add a dependency without a consumer.
   - Correlation IDs are already returned in every API error envelope.
3. **Health endpoints are dropped for now.** There is no orchestrator or load balancer to probe them.
   - Startup already fails fast on a non-loopback binding and on migration errors.
   - A readiness endpoint may be added on loopback only if a launcher or WebView2 shell needs it.
4. **CI (GitHub Actions) is dropped for now.** The repository has no remote CI target configured.
   - The local gate is the README build/test sequence, with warnings as errors.
   - Adding CI later requires no architectural change.
5. **The generated OpenAPI client is dropped for now.** The frontend uses a small hand-written `apiRequest` with one typed call per endpoint.
   - The OpenAPI document is still produced in the Development environment only.
   - Reason: nine endpoints. A generator and its output would add more surface than they remove.

## Consequences
- Fewer dependencies and less indirection. `ARCHITECTURE.md` and `API.md` now describe the actual design.
- If the endpoint count grows substantially (Phase 2+), revisit item 5 first, then item 1.
- Each dropped item can be reinstated by a new ADR without reworking the layering, because the Domain/Application/Infrastructure/Api boundaries are unchanged and enforced by tests.
