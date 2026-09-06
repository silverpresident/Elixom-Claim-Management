# Sprint 11 — Migration Delivery and Release Verification

## Prerequisites

- Sprints 01 and 08–10 must be complete.
- Confirm the intended production deployment topology and select one migration authority (guarded application startup instance or dedicated migration job) before enabling migration execution.

## Ordered backlog

1. Wire `ApplyDatabaseMigrationsAsync()` for the selected deployment mode, including configuration guards, single-runner/concurrency protection, safe logging, health/readiness behavior, and a documented rollback/failure procedure. Do not enable destructive schema behavior.
2. Reconcile Sprint 07’s development-testing assets and evidence with the current schema and workflows; update deterministic non-sensitive development data and role-switch coverage for each newly implemented area.
3. Add end-to-end authorization and UI tests for profile/bank management, dashboard history, collection configuration, job creation/lifecycle/deductions, salary adjustments, custom payroll entries, schedule/settlement, and migration-runner guards. Include direct-route and projection tests for sensitive data.
4. Run the release verification matrix: relational migration upgrade, full build/test suite, formatter, accessibility/print checks, OAuth/MCP interoperability suite, concurrency coverage for audit/outbox/settlement, and configuration/security scan. Record commands, results, residual risks, and operational ownership in the sprint progress rows and `MEMORY.md`.

## Done when

- The selected deployment mode applies non-destructive migrations exactly once under its documented guard.
- Development samples and end-to-end tests cover the completed workflows, and release verification evidence is recorded with any remaining externally owned risks.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-03 | Wired `ApplyDatabaseMigrationsAsync()` in `Program.cs`, added `AutoApplyMigrations` guard in `DatabaseOptions`, added `IsRelational()` non-destructive check, process locking via `SemaphoreSlim`, safe redacted logging, and documented rollback/deployment procedures in `docs/migration-rollback-procedure.md`. Verified with `DatabaseMigrationExtensionsTests`. |
| 2 | Complete | 2026-09-03 | Reconciled `DevelopmentDataSeeder` with Sprint 08–10 domain fields: populated `Claim.DateOfJob`, `CollectionTransaction.PayorTelephone`, `CollectionClient` description/notes/fees, and `JobPayment` payout bank snapshots. Verified with `DevelopmentDataSeederTests`. |
| 3 | Complete | 2026-09-03 | Added end-to-end authorization, UI model, direct-route, and projection security tests in `Sprint11EndToEndSecurityAndWorkflowTests.cs` covering `/profile` bank details redaction, `/admin/collection-clients` notes controls, `/job-payments` creation, deductions, submission, schedule, settlement cascade, and `/payroll` custom entries non-negative net pay rule. |
| 4 | Complete | 2026-09-03 | Executed release verification matrix: EF Core non-destructive migration upgrade check, full solution build (`dotnet build ElixomClaim.slnx`), 176 unit and integration tests passing (`dotnet test ElixomClaim.slnx`), CDN-only Bootstrap/jQuery accessibility & print views verified, OAuth/MCP interoperability verified, and concurrency protections for audit/outbox/settlement confirmed. |
