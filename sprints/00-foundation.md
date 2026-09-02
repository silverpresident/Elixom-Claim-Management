# Sprint 00 — Foundation

## Goal

Create a reliable, attractive base that later business work can extend without structural rework.

## Ordered backlog

1. Create the solution and four projects under `src/`; remove every `Class1.cs`; add central package/version management if adopted.
2. Add the Lib DI extension, single-company Azure SQL `DbContext`, `dbclaim` default schema, entity base conventions, UTC/time abstraction, JMD `decimal(18,2)` exact-value/no-additional-rounding policy, health checks, and guarded migration/startup-seed extension.
3. Establish migrations workflow: local developer database guidance, production single-runner policy, migration test, and no automatic destructive schema action.
4. Create the MVC layout, responsive navigation, status-badge component, validation summary, accessible empty/error states, footer, SVG favicon, and print stylesheet. Reference Bootstrap 5.3 and jQuery 3.7 via CDN with SRI where available.
5. Add a meaningful privacy page under Jamaican law with nine-year retention and `privacy@elixom.com` as the privacy/support contact.
6. Introduce typed/redacted configuration options and validation for database, Google, email, and OAuth settings; document secrets setup.
7. Configure structured `ILogger<T>` logging, correlation IDs, error handling, health/readiness endpoints, test-data builders/anonymized seed data, and baseline test infrastructure.
8. Establish ADR templates/index, `.github` workflow skeleton, branch protections/review expectations, GitHub Actions build/test/security-scan gates, and environment/secrets/migration deployment policy.

## Done when

- The solution builds/tests cleanly, renders the shell on desktop/mobile, and has no local Bootstrap/jQuery or PDF dependency.
- A migration creates only `dbclaim` objects and configuration failures are understandable without leaking secrets.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-02 | Solution ElixomClaim.slnx & 4 projects in src/ created; Class1.cs/wwwroot/lib removed; references added. Command: `dotnet test ElixomClaim.slnx` passed (2 tests). |
| 2 | Complete | 2026-09-02 | Lib DI extension, ApplicationDbContext (dbclaim schema, decimal(18,2) exact JMD), ISystemClock, Result<T>, health checks, and DatabaseMigrationExtensions. Command: `dotnet test ElixomClaim.slnx` passed (9 tests). |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
| 7 | Not started | — | — |
| 8 | Not started | — | — |
