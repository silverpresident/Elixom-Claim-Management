# Claude Specification Completeness Report

**Assessment date:** 2026-09-10

**Source specification:** [`context/claude-specs.md`](../context/claude-specs.md)
**Repository state assessed:** current checked-out worktree, commit `ec52cb9` (`fix(email): hide Bcc from delivery views`)

## Executive conclusion

The implementation is substantially complete against the product specification. The core claim, clearing-house, job-payment, salary/payroll, Google SSO, custom OAuth, durable notification, MVC, official MCP, and versioned API flows are present and have extensive unit/integration coverage.

It is **not release-ready**. Sprint 13 remains incomplete: the literal audit/email migration has not been rehearsed on a production-shaped Azure SQL restore; the typed audit migration has remaining legacy callers; email-header handling has residual compatibility/coverage gaps; release assurance, independent OAuth/MCP review, and staging evidence are outstanding. In addition, the current full test run is not green: one Web integration test has an assertion that conflicts with the newer Bcc-only system-copy behavior.

## Verification performed

```bash
dotnet test ElixomClaim.slnx --no-restore --no-build --logger "console;verbosity=minimal"
```

| Test project | Passed | Failed | Total | Result |
| --- | ---: | ---: | ---: | --- |
| `ElixomClaim.Lib.Tests` | 129 | 0 | 129 | Passed |
| `ElixomClaim.Web.Tests` | 108 | 1 | 109 | Failed |
| **Total** | **237** | **1** | **238** | **Not green** |

The failed test is `ApiEndpointIntegrationTests.EmailTemplatesApi_QueuesOnlyApprovedRecipientsIdempotently` at line 358. It expects two outbox records but observed one. The current queue implementation deliberately sends one visible-recipient message and stores the configured system-copy address in its `Bcc` header. The test must be reconciled with the approved Bcc-only design before a green verification claim can be made.

This was a read/audit exercise except for replacing this report. No application behavior, migration, or sprint progress record was changed.

## Requirement assessment

| Specification area | Status | Evidence and assessment |
| --- | --- | --- |
| .NET 10 / ASP.NET Core MVC / C# solution | Implemented | `ElixomClaim.Lib`, `ElixomClaim.Web`, and matching xUnit projects exist. The projects target .NET 10; no scaffold `Class1.cs` remains. |
| Lib/Web separation | Implemented | Entities, EF context, services, and shared authorization are in Lib. MVC, Google authentication wiring, OAuth endpoints, MCP transport, and hosted services are in Web. |
| Azure SQL and `dbclaim` | Implemented, deployment evidence pending | EF Core SQL Server configuration, `HasDefaultSchema("dbclaim")`, migrations, relational migration tests, and guarded production migration coordination are present. A release-environment migration rehearsal is still blocked. |
| Guid, UTC, money, display records | Implemented | Guid technical keys are used consistently; monetary mapping uses `decimal(18,2)`, JMD domain handling, UTC fields, and sequence-backed user-facing record numbers. This is a documented refinement of the source spec, which permitted either int or Guid IDs. |
| Google-only provisioned sign-in | Implemented | Google-only authentication, active provisioned-user validation, no password flow, and a bootstrap `DefaultAdminEmail` promotion/seed path are present and tested. |
| Hierarchical single role model | Implemented | `Blocked`, `User`, `Teller`, `Manager`, `Accountant`, and `Administrator` are represented as a hierarchy through shared policies/handlers, retaining ownership checks in services. |
| Claims | Implemented | Draft creation, owner edit/soft delete, submission, accept/reject, public/private append-only comments, payment status, job attachment, payment history, MVC/API/MCP routes, and service tests are present. |
| Clearing-house collections | Implemented | Client configuration, active-user assignments, scoped options, custom immutable snapshots, teller-local date conversion, payor details, system-owned fee snapshot, status lifecycle, reissue, HTML receipt, and print route are present. |
| Job payments | Implemented | The user-or-client payee invariant, same-client collection constraint, processing-only line/deduction edits, calculated fees/totals, submit/schedule/paid lifecycle, atomic settlement cascade, idempotent payout queueing, and linked adjustment/reversal model are implemented. |
| Salary and payroll | Implemented | Definitions, adjustments, deterministic recurrence planning, inclusive date bounds, generated locked entries, custom-entry net-pay guard, submission-created job payment, and thin hosted scheduling are implemented with tests. |
| Durable outbox and email providers | Mostly implemented | Lib exposes `IEmailSender`; SMTP and ACS senders are configuration-selected. Outbox retry/backoff, per-attempt `EmailLog`, idempotency, and optional-payor handling are implemented. Header-model migration and regression coverage remain incomplete. |
| Custom OAuth 2.0 server | Mostly implemented | Registration, authorization, consent, code exchange, PKCE S256, strict redirect validation, token/refresh handling, revocation, scopes, rate limits, audit, and bearer identity projection are implemented in-house. Independent protocol/security review and release evidence remain open. |
| Official MCP server | Implemented with assurance gap | Official stateless Streamable HTTP is mapped at `/mcp`, protected by custom bearer auth, `mcp:access`, and rate limiting. Six domain tool classes use constrained DTOs and shared services. Expired/revoked-token transport evidence remains a Sprint 13 item. |
| MCP email/operation constraints | Implemented | Email tools are limited to approved receipt/payment-summary composition/queueing; adapters do not accept arbitrary recipients or bodies. Operations create auditable idempotent durable requests rather than invoking workers directly. |
| Versioned REST API/OpenAPI | Implemented | Approved resources live at `/api/v1`, require separate `api:access`, use actor/ownership checks and command idempotency, and publish scope-protected `/openapi/v1.json` excluding MVC/MCP routes. |
| Audit trail and immutability | Partial | Append-only Azure SQL protection and broad mutation/OAuth/MCP audit coverage exist. The literal `EntityType`/`EntityId`/`OccurredAtUtc` model is present, but migration completion and all typed callers/projections are not complete. |
| Role dashboards and workflows | Implemented | User claims/payment history, teller collection work, manager review/attachment, accountant scheduling/settlement and payroll workspaces, and administrator user/configuration/audit screens are present. |
| HTML-only printable output | Implemented | Collection and job-payment views provide responsive print-focused HTML. No PDF feature was found. Job-payment print includes semantic item tables and subtotals. |
| Privacy, CDN assets, favicon | Implemented | A substantive privacy page is linked from the footer; Bootstrap/jQuery use CDN links; no local Bootstrap/jQuery distribution was found; a project SVG favicon is present. |
| Logging requirement | Mostly implemented | Controllers, most services, sender adapters, workers, and tool adapters inject `ILogger<T>`. `ApprovedEmailPreviewService`, `McpToolActorAccessor`, `SalaryRecurrencePlanner`, and `SystemClock` do not; pure utilities can be formally exempted, while preview/actor boundaries need a deliberate logging decision. |
| Agent guidance / durable memory | Implemented | Root `AGENTS.md` and `MEMORY.md` describe delivery order, architecture, security, test, and handoff expectations. |

## Detailed differences, variations, and incomplete work

### Literal audit persistence is only partially migrated

The specification requires `OccurredAtUtc`, `EntityType`, and `EntityId`. The current `AuditRecord` model, mappings, migration `20260909124540_StructuredAuditAndEmailHeaders`, structured query projections, and append-only trigger support this model. The migration is additive and retains/backfills legacy source columns for one release, as documented in ADR 0008 and the literal migration runbook.

The old string-target compatibility contract has not been fully eliminated. `IAuditService` retains an obsolete `LogAsync(..., string target, ...)` overload, and current production calls still use it in `AdminController` (user role/status), `ProfileController` (bank details), and `ActorResolver.LogAuditAsync` (shared API/MCP adapter helper). The compatibility entity properties are not mapped, so this is incomplete source-contract migration rather than a schema regression. Sprint 13 item 2 should move the remaining callers to `AuditEntity`, then add attribution/projection evidence for every remaining surface.

### Literal email headers and Bcc-only copies are substantially, not completely, integrated

`EmailOutboxItem` and `EmailLog` have `To`, `From`, `Cc`, and `Bcc`. SMTP and ACS adapters send `Cc`/`Bcc`; normal collection-receipt and payout-summary queues set the configured sender and Bcc-only system copy; administrator delivery views render only visible `To`/`From` information. This is a privacy improvement over the source wording that allowed CC or BCC.

Residual issues are visible in the code:

- Obsolete non-persisted `Recipient` compatibility properties remain, and the collection missing-payor skip path still initializes records through them.
- Missing-payor skipped outbox/email-log records do not set `From` or `Bcc`. They record an intentional non-send, but need an explicit literal-header decision and test.
- `EmailLog` persists Bcc for authorised delivery evidence while administrator views hide it. Sprint 13 must prove it is absent from unauthorised projections, previews, operational logs, audit payloads, and failure responses.
- The failing integration test still reflects the former separate system-copy recipient model.

This is therefore **Bcc behavior implemented in main delivery paths, migration/projection/test closure pending**, not a fully complete email-header implementation.

### Payout presentation is ahead of notification parity

`Views/JobPayments/Print.cshtml` implements accessible, responsive, captioned tables for claims, collections, payrolls and ordered entries, deductions, and final calculation, with subtotals. Internal notes and bank details are excluded from print output.

The paid notification must also provide appropriate itemized payout information. `JobPaymentService.ComposePayoutHtml` includes payout and linked payroll information, but Sprint 13 still calls for evidence of equivalent table/subtotal markup, authorised bank/recipient rules, and redaction regression coverage. Treat print fidelity as complete and notification fidelity as partial until that evidence is finished.

### Manager audit scope is implemented in MVC, but complete surface evidence remains open

`AdminController.AuditLogs` limits non-administrators to `Claim`, `CollectionTransaction`, and `JobPayment` records and strips before/after JSON. This matches the documented least-privilege decision: Managers receive metadata only; payroll, salary, user administration, OAuth/security, email delivery, and state data remain Administrator-only.

The remaining concern is assurance scope: Sprint 13 still requires pagination plus API/MCP authorization and redaction review/tests. This should not be presented as a completed release control until those paths are demonstrably absent or protected.

### Release controls cannot be completed locally

The following remain external release blockers:

- independently observed rehearsal of the additive audit/email migration on a production-shaped Azure SQL restored copy;
- a named release owner, single migration-runner evidence, safe email-provider test account, backup/restore/PITR evidence, and go/no-go record;
- independent OAuth/MCP threat-model and interoperability review, remediation/retest, and sign-off; and
- staging smoke tests for Google sign-in, PKCE/refresh/revocation, scope isolation, notification delivery, audit immutability, retry/recovery, and retention.

Runbooks and ADRs exist, but documentation is not execution evidence.

### Source-structure variation: flat controllers rather than MVC Areas

The source structure illustrates `Areas/Admin`, `Areas/Manager`, `Areas/Teller`, and `Areas/Accountant`. The current implementation uses role-oriented controllers/views rather than ASP.NET MVC Areas. Authorization policies and route behavior provide the functional separation, so this is a maintainability variation rather than a demonstrated functional deficiency.

### Compatible enhancements beyond the source specification

| Source baseline | Current implementation | Assessment |
| --- | --- | --- |
| ID may be int or Guid | Guid technical IDs plus durable display sequences | Consistent refinement. |
| Basic email send/log expectation | Transactional outbox, retries, idempotency, per-attempt logs, durable wake-up operations | Reliability improvement. |
| No paid-record correction workflow specified | Auditable linked adjustment/reversal workflow | Financial-record protection improvement. |
| Suggested MCP routes/tools | Official SDK Streamable HTTP MCP at `/mcp`, plus separately scoped REST API | Standards-conformant extension. |
| System copy can be CC or BCC | Bcc-only system copy in normal queue paths | Privacy-preserving decision; test closure remains. |

## Completion priorities

1. Correct/reconcile the failing API integration assertion with the Bcc-only system-copy contract, then rerun the full suite.
2. Finish Sprint 13 items 2–3: replace remaining string-target audit calls, retire header compatibility paths, define missing-payor header semantics, and add redaction/projection/provider/retry coverage.
3. Finish payout-notification table/subtotal and authorised-bank-data regression evidence; retain the complete print work.
4. Complete Manager audit pagination and API/MCP surface assurance, then resolve or formally exempt remaining logger exceptions.
5. Execute the production-shaped migration rehearsal, independent OAuth/MCP review, staging smoke tests, backup/restore checks, and release go/no-go process.

## Delivery-state note

`MEMORY.md` records Sprint 12 as complete and Sprint 13 as active/partially blocked. This assessment agrees with that ordering. The software has a broad working implementation, but the remaining literal-model migration work, test failure, and release controls prevent a complete or production-ready declaration.
