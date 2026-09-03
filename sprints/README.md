# Implementation Backlog

Execute these sprints in order. A later sprint may refine presentation, but it must not replace the security, transaction, or lifecycle rules established earlier. Each ticket is complete only when its acceptance checks pass, relevant tests are added, audit behavior is verified where applicable, and `MEMORY.md` is updated.

| Order | Sprint | Outcome |
| --- | --- | --- |
| 00 | [Foundation](00-foundation.md) | Buildable solution, data foundation, UI shell, operational guardrails. |
| 01 | [Identity & security](01-identity-security.md) | Provisioned Google sign-in, policies, audit, OAuth hardening. |
| 02 | [Claims](02-claims.md) | Safe end-to-end claimant and management workflow. |
| 03 | [Clearing house](03-clearing-house.md) | Collection capture, receipts, client management. |
| 04 | [Job payments](04-job-payments.md) | Controlled grouping, payout execution, notifications. |
| 05 | [Salary & payroll](05-salary-payroll.md) | Recurrence engine and payroll-to-payment flow. |
| 06 | [MCP & release readiness](06-mcp-release.md) | User-scoped MCP plus accessible, production-ready release. |
| 07 | [Development testing](07-development-testing.md) | Safe local in-memory sample data and role-switch testing workflow. |
| 08 | [MCP transport and OAuth hardening](08-mcp-oauth-hardening.md) | Standard MCP transport, durable operations, and hardened OAuth abuse/security boundaries. |
| 09 | [Domain data completion](09-domain-data-completion.md) | Complete required fields, identifier consistency, mappings, migrations, and relational coverage. |
| 10 | [Web workflow completion](10-web-workflow-completion.md) | Complete user, job-payment, payroll, and navigational MVC workflows. |
| 11 | [Deployment and release verification](11-deployment-and-release-verification.md) | Guarded migration delivery and final end-to-end/release evidence. |

## Global acceptance bar

- No secrets in source control; configuration is validated at startup.
- All mutable workflows use server-side authorization, validation, transactions, audit logging, and idempotency where they cause external effects.
- Bootstrap/jQuery are CDN-hosted; no PDF feature is added.
- Critical lifecycle, permission, and money-calculation paths have automated tests.

## Progress record convention

Each sprint file has a mutable **Progress** table. Before implementation, claim exactly one numbered backlog item by changing it from `Not started` to `In progress`. On completion or handoff, record the date, concise result, affected files/tests, and verification command/outcome. `Blocked` rows must name the blocker and the next safe action. This record, together with `MEMORY.md` and the current Git diff, prevents duplicate or overlapping work.
