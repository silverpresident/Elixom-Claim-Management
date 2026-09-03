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
| 1 | Not started | — | Requires deployment-topology decision. |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
