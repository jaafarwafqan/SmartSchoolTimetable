# ADR 0015: Design-system enforcement and EF migration tooling

- Status: Accepted (required by the owner's Phase 1.4 and Phase 2 instructions)
- Date: 2026-10-03

## Context
DESIGN_SYSTEM.md section 12 requires automated enforcement:
- Stylelint rules for colours, logical properties and fonts.
- ESLint rules.
- A contrast test.
- Playwright accessibility (axe) and screenshot checks.

New EF Core migrations must also be generated with their Designer files. Two earlier migrations were hand-written without them because `dotnet-ef` was not available.

## Decision
All four tools below are development-only and are not shipped in the application.

| Tool | Version | Licence | Purpose |
|---|---|---|---|
| `stylelint` | 17.16.0 | MIT | CSS lint: `color-no-hex` (except `tokens.css`), no `font-family` or physical properties, no `px` font sizes |
| `stylelint-use-logical` | 2.1.3 | CC0-1.0 | The logical-properties plugin named in DESIGN_SYSTEM.md 12.1 (rejects `margin-left`, `left`, `float: left`, `text-align: right`, ...) |
| `@axe-core/playwright` | 4.13.0 | MPL-2.0 | axe accessibility scan in Playwright (no serious/critical violations allowed) |
| `dotnet-ef` (local tool, `.config/dotnet-tools.json`) | 9.0.20 | MIT | Generates EF Core migrations with Designer files; restored with `dotnet tool restore` |

- **Security:** all run only on the developer machine, during lint, test or migration generation. `npm audit` reports 0 vulnerabilities. The axe licence (MPL-2.0) covers only the test harness.
- **Maintenance:**
  - Stylelint and axe-core are actively maintained (releases in 2026).
  - `stylelint-use-logical` is maintained by csstools and supports Stylelint up to 17.
  - `dotnet-ef` ships with EF Core and is pinned to the runtime version.
- **Alternatives considered:**
  - Stylelint core rules only (`property-disallowed-list`). These are kept as a second guard, but the logical-properties plugin gives better messages and was named by DESIGN_SYSTEM.md.
  - Hand-rolled contrast or a11y checks: less complete than axe.
  - A global `dotnet-ef` install: changes the developer's global environment and is not versioned with the repo.

## Consequences
- `npm run lint` runs ESLint and Stylelint; violations fail it.
- A test (`EfModelHasNoPendingChangesVersusTheLatestMigration`) fails if the EF model drifts from the latest migration.
- New migration command:
  ```
  dotnet tool restore
  dotnet ef migrations add <Name> -p src/SmartSchoolTimetable.Infrastructure -s src/SmartSchoolTimetable.Infrastructure
  ```
