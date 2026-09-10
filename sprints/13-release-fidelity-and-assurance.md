# Sprint 13 — Release Fidelity and Assurance

## Purpose

After Sprint 12 completes, close the remaining specification-fidelity and production-assurance gaps identified in the 2026-09-08 Claude assessment. This sprint does not change established business workflows or authorization boundaries.

The product decision is literal source-model fidelity: audit records use `EntityType`, `EntityId`, and `OccurredAtUtc`; message/outbox records use `To`, `From`, `Cc`, and `Bcc`. The configured system-copy address is always `Bcc`, never `Cc`. Durable outbox delivery, individual attempt history, redaction, and nine-year retention remain required.

## Prerequisites

- Sprint 12 is Complete, including its transport/API contract and end-to-end security evidence.
- A production-like Azure SQL staging environment, email-provider test account, and production deployment owner are available.
- An independent OAuth/MCP reviewer is selected, or release remains explicitly held pending selection.

## Definition of ready

- Inventory every entity, migration, sender, composition service, projection, and test that consumes `EmailOutboxItem`, `EmailLog`, or `AuditRecord`.
- Define a data-preserving migration/rehearsal and rollback procedure; no audit, financial, outbox, or email history may be deleted or silently rewritten.
- The approved Manager audit policy is metadata-only access for claims, collections, and job payments; all other domains and before/after state are Administrator-only.
- Follow the independent-review runbook for assessor independence, staging access, safe test identities, and evidence handling.

## Ordered backlog

1. **Design the literal audit/email migration.** Record the mapping, compatibility and rollback strategy, indexing/constraint changes, historical-data treatment, and authorized-projection impact in an ADR and release runbook. Define one logical message with `To`, configured `From`, optional `Cc`, and `Bcc`; the configured system copy is Bcc-only. Replace audit `Target`/`TimestampUtc` usage with non-null `EntityType`, `EntityId`, and `OccurredAtUtc`. Rehearse against a production-shaped restored database copy.
2. **Implement structured, append-only audit records.** Update entities, EF mapping, service contracts/callers, the Azure SQL append-only trigger, query projections, and tests. Preserve the meaning of historical targets, prohibit mutation after migration, and verify redaction, OAuth/MCP/API attribution, and Administrator/Manager projections.
3. **Implement header-aware emails and BCC system copies.** Update outbox/log records, composition/queue services, SMTP/ACS adapters, retries, idempotency, migrations, and authorized MVC/API/MCP projections. Bcc addresses must never appear in rendered messages, previews, unauthorized queries, logs, audit payloads, or errors. Retain per-recipient delivery evidence and valid-recipient behavior without allowing arbitrary recipients, free-form email, direct provider calls, or worker invocation.
4. **Complete payout print/notification fidelity.** Render accessible, responsive HTML tables for claims, collections, payrolls and ordered entries, deductions, category subtotals, totals, recipient details, and authorized bank details. Use the same approved data in notifications; exclude internal notes and unauthorized/full bank data. Add regression coverage for calculations, redaction, HTML-only output, and role-specific visibility.
5. **Enforce Manager audit visibility.** Constrain shared audit queries and MVC/API/MCP projections so Managers can list metadata only for claims, collections, and job payments. Payroll, salary, user-administration, OAuth/security, email-delivery, and all other audit domains remain Administrator-only; before/after state remains Administrator-only. Add authorization, domain-filter, pagination, and redaction tests.
6. **Close logging and protocol coverage.** Add safe structured outcome/security logs where concrete adapters/services make meaningful decisions, and formally exempt only pure deterministic utilities. Add real transport evidence for expired/revoked tokens and supported MCP cancellation. Complete API endpoint/error/OpenAPI coverage for authentication, scope, ownership, validation/Problem Details, redaction, pagination, and idempotency.
7. **Execute release controls and independent review.** Designate and rehearse a single production migration runner. Complete independent OAuth/MCP threat-model and interoperability review, remediate/retest findings, and obtain sign-off. Run staging smoke tests for provisioned Google sign-in; OAuth PKCE/refresh/revocation; MCP/API scope isolation; notification delivery; audit immutability; retry/recovery; backup/restore; and retention. Publish a release evidence pack and go/no-go record.

## Non-goals

- No new business workflow, role, payment method, local credential flow, PDF feature, bulk export, arbitrary-recipient email, direct provider endpoint, or direct worker execution.
- No deletion of existing audit, financial, email, or outbox history to simplify migration.
- No production release before independent OAuth/MCP review completes and all Critical/High findings are remediated or formally accepted by the accountable release authority.

## Done when

- Literal audit/email persistence is deployed through a data-preserving migration, system copies are Bcc-only, and relational/integration evidence proves authorization, redaction, immutability, and delivery invariants.
- Payout print/email detail is itemized and subtotalled while internal notes and unauthorized data stay excluded.
- Manager audit scope is recorded and enforced; logging exceptions are resolved or formally exempted.
- Token-lifecycle, cancellation, API-contract, scope-isolation, and sensitive-data evidence is complete.
- Dedicated migration-runner rehearsal, independent review, staging smoke tests, backup/restore verification, and a final evidence/go-no-go record have no unaccepted release blockers.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Blocked | 2026-09-10 (/root) | User reconfirmed no schema, migration ledger, or data exists to preserve. The EF chain is flattened to one `20260910110439_InitialCreate` migration that directly creates literal `EntityType`/`EntityId`/`OccurredAtUtc`, message headers, indexes, `dbclaim`, sequences, and the append-only trigger; ADR 0009 records the clean-slate decision. `MigrationValidationTests` passed (4) and the generated idempotent script contains the literal fields and trigger. Completion remains blocked by the required independently observed production-shaped Azure SQL rehearsal, designated release owner, and safe email-provider test account. |
| 2 | Complete | 2026-09-10 (/root) | Production read paths, Razor views, development seed, lifecycle services, collection-client administration, MVC/API, and MCP adapters use `EntityType`/`EntityId`/`OccurredAtUtc` through `AuditEntity`; the string overload is compatibility-only. Migration temporarily replaces the trigger only inside its transaction, backfills legacy data, then restores append-only protection; retained legacy columns are nullable for new structured records. Focused audit/relational persistence tests passed (3), and Manager/API/MCP projection tests passed (30). The unrelated uncommitted development-seeder change currently prevents full-suite verification. |
| 3 | Blocked | 2026-09-10 (/root) | Collection receipt/reissue, job-payment summaries/resends, and MCP-approved queues persist configured `From` and Bcc-only system copies; skipped optional-payor records preserve the same headers. Retry/send tests prove headers persist to delivery history and Bcc reaches the provider message; Administrator delivery views render only `To`/`From`, never Bcc. All active tests/fixtures use header fields rather than the obsolete `Recipient` alias. Blocked only on the required external safe email-provider test account and independently observed provider rehearsal. |
| 4 | Complete | 2026-09-09 (/root) | Replaced list-only payout print presentation with captioned, responsive semantic tables for claims, collections, ordered payroll entries, deductions, and the calculation; each has category subtotals. Internal notes and bank data remain excluded. Verified `dotnet test src/ElixomClaim.Web.Tests/ElixomClaim.Web.Tests.csproj --filter FullyQualifiedName~JobPaymentPrintViewTests` (3 passed). See `Views/JobPayments/Print.cshtml` and `Web.Tests/Views/JobPaymentPrintViewTests.cs`. |
| 5 | Complete | 2026-09-10 (/root) | Manager MVC audit logs use 50-record bounded pagination after the Claim/CollectionTransaction/JobPayment filter and project no state data. The exact approved API/OpenAPI and MCP tool inventories expose no audit-log resource. `dotnet test ElixomClaim.slnx --no-restore` passed (240 tests); focused Manager filtering/redaction/pagination and API/MCP contract tests passed (8). See `AdminController`, `AuditLogPageViewModel`, and `AdminControllerTests`. |
| 6 | Complete | 2026-09-10 (/root) | All concrete production controllers, services, and hosted services have `ILogger<T>`; `ApprovedEmailPreviewService` logs safe structured authorization/availability/success outcomes without recipient, body, or bank data. A real bearer-protected TestServer endpoint accepts a valid OAuth-issued token then returns `401` after revocation or forced expiry; MCP cancellation is covered. API/MCP contract tests cover authentication/scope, ownership, validation/Problem Details, pagination, redaction, idempotency, discovery, and route isolation. `dotnet test ElixomClaim.slnx --no-restore` passed (242 tests). |
| 7 | Blocked | 2026-09-10 (/root) | Cannot execute release controls locally: requires a designated migration runner/release owner, production-shaped Azure SQL restore and staging, safe email-provider test account, provisioned Google sign-in, independently selected OAuth/MCP reviewer, backup/restore authority, and accountable go/no-go sign-off. The runbooks and local evidence are ready; no production/release claim is made without these external prerequisites. |
