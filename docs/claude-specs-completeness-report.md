# Claude Specification Completeness Report

**Re-evaluated:** 2026-09-07

**Source specification:** [`context/claude-specs.md`](../context/claude-specs.md)
**Method:** Source, migrations, routes, Razor views, tests, configuration, sprint ledger, and runtime wiring were reviewed. Findings are based on executable source rather than sprint-status assertions.

## Overall conclusion

The implementation is materially more complete than the 2026-09-03 review. Missing domain fields, profile management, manager/accountant workflows, audit trigger, development data, OAuth consent/lifetime work, rate limiting, and migration startup wiring have largely been added. Sprint 12 now also establishes the standard MCP transport at `/mcp`, with a separate `api:access` scope and shared authenticated-actor resolver.

It is still **not complete or release-ready**. The standard MCP transport is mapped and the legacy `/mcp/*` controllers are gone, but tool hardening is in progress and the separately contracted `/api/v1` REST API is not implemented. A second high-impact problem is incorrect collection-fee sourcing/allocation. The full relational suite remains blocked by an empty-database migration failure; multi-instance migration locking, email/audit-schema fidelity, and universal logging/layering requirements are also incomplete.

## Material change since the previous review

| Former gap | Current evidence | Re-evaluation |
| --- | --- | --- |
| Mixed `Guid`/`long` business IDs | Business entities and MVC routes now use `Guid`; see [`ClaimEntities.cs`](../src/ElixomClaim.Lib/Entities/ClaimEntities.cs). | Resolved for business-domain records. |
| Claim dates, user-bank data, collection fields, payout snapshots | New entities/mappings/forms/migration provide these fields. | Resolved / substantially resolved. |
| Job/payment and payroll UI gaps | [`JobPaymentsController.cs`](../src/ElixomClaim.Web/Controllers/JobPaymentsController.cs) and [`PayrollController.cs`](../src/ElixomClaim.Web/Controllers/PayrollController.cs) now expose core workflows. | Substantially resolved. |
| Mutable audit records | Migration [`20260903090000_AddAuditRecordAppendOnlyTrigger.cs`](../src/ElixomClaim.Lib/Migrations/20260903090000_AddAuditRecordAppendOnlyTrigger.cs) creates an Azure SQL UPDATE/DELETE trigger. | Resolved at the SQL boundary. |
| OAuth consent/lifetime/redirect/rate-limit gaps | OAuth service, consent persistence, strict redirect validation, and rate-limit configuration exist. | Substantially resolved. |
| Process-local MCP operation records | [`OperationRecordService.cs`](../src/ElixomClaim.Lib/Services/OperationRecordService.cs) persists idempotency records. | Resolved for durable records. |
| No standard MCP transport or shared actor boundary | [`Program.cs`](../src/ElixomClaim.Web/Program.cs) maps official stateless Streamable HTTP at `/mcp`; `McpToolActorAccessor` resolves callers through `IActorResolver`; legacy controllers are removed. | MCP transport resolved. Tool hardening is in progress; `/api/v1` remains unimplemented. |
| Profile/bank, collection capture/receipt, client-bank, and record-number usability gaps | Sprint 12 items 2a–2f add optional user display names; bank branch/account-type fields; traditional printable receipts; custom collection purpose/amount snapshots; client-bank metadata; durable user-facing sequence numbers; and per-run Development collection samples. | Mostly resolved; clean relational migration verification currently fails (finding 8). |

## Requirement assessment

| Area | Status | Evidence / assessment |
| --- | --- | --- |
| .NET 10 MVC, Lib/Web/test split, EF Core, `dbclaim`, JMD precision | Implemented | Project structure, DbContext, migrations, and model/service tests support the baseline. |
| Google provisioned-user sign-in, bootstrap administrator, hierarchical roles | Implemented | Authentication configuration, validation, policies, and authorization tests support the intended model. |
| Claims lifecycle, ownership, comments, soft deletion, dates | Mostly implemented | [`ClaimService.cs`](../src/ElixomClaim.Lib/Services/ClaimService.cs), entities, MVC pages, and tests cover the workflow. Comment append-only behavior is not a database invariant. |
| Ordinary-user profile, bank details, payment history | Implemented | [`ProfileController.cs`](../src/ElixomClaim.Web/Controllers/ProfileController.cs), Razor view, masking helper, dashboard history, and migration `20260907090000_AddUserProfileDisplayAndBankFields` provide an optional display name plus bank branch-name/account-type fields. |
| Collection clients, complete client bank details, options, capture, receipt/reissue/print | Mostly implemented | Administrator-managed client bank details now require branch name and Savings/Current account type. Capture supports configured suggestions plus immutable custom purpose/amount snapshots; printable receipts show payer, teller display name, method, and browser-local date/time while excluding telephone/fees. Fee behavior is materially incorrect; see finding 2. |
| Job payments, deductions, lifecycle, settlement cascade, adjustment flow | Mostly implemented | Database constraint, shared service, MVC workflows, and lifecycle tests cover the core. Payout presentation remains incomplete. |
| Salary recurrence, generated payroll, adjustments/custom entries, submit-to-job | Implemented | Planner, service, hosted scheduler, MVC actions, and tests are present. |
| Durable email outbox, SMTP/ACS, retry, receipt/payout notifications | Mostly implemented | Outbox/sender implementations work, but the literal email-log/header schema is incomplete. |
| OAuth code + PKCE, rotation/revocation, consent, redirect validation, throttling, transport scopes | Mostly implemented | Core OAuth controls and both `mcp:access` / `api:access` scope definitions exist. `mcp:access` is enforced at `/mcp`; API-scope separation awaits the unimplemented `/api/v1` surface. |
| Standard MCP server transport and registered-tool discovery | Mostly implemented | Official stateless Streamable HTTP is mapped at `/mcp`, protected by Bearer + `mcp:access` + MCP rate limiting, and registers all six domain tool classes with SDK discovery attributes. Tool hardening/integration evidence remains in progress. |
| Audit trail and append-only SQL enforcement | Mostly implemented | Audit service/redaction and the SQL trigger exist. The record shape differs from the specified EntityType/EntityId model. |
| Privacy, CDN frontend dependencies, favicon, HTML-only printing | Implemented | Layout, privacy page, the plum-and-gold SVG favicon, and assets conform. |
| Development-only samples and role switching | Implemented | Seeder and Development-only login route are guarded by environment/configuration. |

## Open differences and defects

### 1. Standard MCP transport is implemented; tool hardening and REST replacement remain — high priority

Sprint 12 item 3 resolves the former transport gap:

- [`Program.cs`](../src/ElixomClaim.Web/Program.cs) registers official `ModelContextProtocol.AspNetCore` stateless Streamable HTTP, registers all six tool classes, and maps the sole MCP endpoint at `/mcp`.
- `/mcp` requires the custom Bearer scheme, an authenticated `mcp:access` user, and the MCP rate-limit policy.
- The six bespoke `Mcp*Controller` routes are removed; each discovered tool class has SDK discovery attributes and resolves its concrete actor through [`McpToolActorAccessor.cs`](../src/ElixomClaim.Web/Mcp/Tools/McpToolActorAccessor.cs) and `IActorResolver`.

The work is not complete:

- Sprint 12 item 4 is in progress in the current worktree. It adds per-invocation audit attribution, cancellation propagation, redacted errors, and safer tool projections; final integration/contract evidence is still required.
- Several tool classes still query `ApplicationDbContext` directly for reads. This may be acceptable for safely projected queries, but violates the stricter requirement that shared services own reusable authorization/business decisions unless those queries are moved behind services or explicitly justified.
- The separate, scope-isolated `/api/v1` REST API promised by ADR 0005 is not implemented (Sprint 12 items 6–8).
- The tool classes still do not inject `ILogger<T>`.

### 2. Collection-fee source and job allocation are wrong — high priority

`CollectionClient` now has `PerJobProcessingFee` and `PerTransactionFee`, but the services do not treat them as financial authority:

- [`CollectionService.cs`](../src/ElixomClaim.Lib/Services/CollectionService.cs) accepts caller-provided `ProcessingFee` instead of deriving/snapshotting the client’s configured per-transaction fee.
- [`JobPaymentService.cs`](../src/ElixomClaim.Lib/Services/JobPaymentService.cs) sums collection processing fees into `ClientProcessingFee` and sets `TotalTxnProcessingFee = 0m`.
- `PerJobProcessingFee` is absent from the calculation.

This reverses the required semantics: a client per-job fee should populate `ClientProcessingFee`, collection transaction fees should populate `TotalTxnProcessingFee`, and both must reduce `TotalPaid`. It can produce incorrect payment totals and lets input fees diverge from configuration.

### 3. Migration single-runner protection is process-local — high priority for production

[`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) now runs migrations at startup but protects execution with a static `SemaphoreSlim`. That works only inside a single process; it cannot coordinate two Azure-hosted application instances or deployment jobs against one database.

The documented requirement calls for a single migration runner/instance. Enforce that topology externally, or use a database/distributed lock. `AutoApplyMigrations` defaults to true, so an explicit production opt-in/migration authority is still advisable.

### 4. Email log remains a partial schema match

`EmailLog` now contains `SentAtUtc`, but still has only a single `Recipient`; it lacks the source specification's `From`, `Cc`, and `Bcc` fields. System copies are sent as separate messages, which is operationally valid but not header-equivalent or a literal audit-schema match.

### 5. Audit record structure is a partial match

The trigger resolves the material immutability gap. [`AuditRecord.cs`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs) nevertheless uses a single string `Target` instead of distinct `EntityType` and `EntityId` fields. This is workable but weaker for structured reporting than the stated model.

### 6. Payout output is less detailed than requested

The payout service includes claims, collections, deductions, and headline totals in HTML. The print view is comparatively minimal and uses lists rather than requested itemized tables with explicit category subtotals. New payout-bank snapshots improve correctness, but presentation completeness is only partial.

### 7. Required logging coverage is incomplete

Controllers without `ILogger<T>` include `AdminController`, `ClaimsController`, `HomeController`, `JobPaymentsController`, `ManagerClaimsController`, `ProfileController`, and all `Mcp*Controller` classes. The specification says every controller, service, and hosted service should inject structured logging.

### 8. Clean relational migration baseline fails — high priority

The current full test run cannot establish the required relational persistence baseline:

- `AuditRecordRelationalPersistenceTests.AuditRecords_RejectUpdatesAndDeletesAtTheSqlServerBoundary` fails during `MigrateAsync` with SQL Server error: `The specified schema name "dbclaim" either does not exist or you do not have permission to use it.`
- The result is **117 passed / 1 failed** in `ElixomClaim.Lib.Tests`; `ElixomClaim.Web.Tests` passes **70 / 70**.

Sprint 12 item 2a records the same clean-SQL baseline migration defect as separately blocked. Until the initial migration/schema creation order is repaired and this relational test passes from an empty database, migration and append-only-audit evidence is incomplete.

### 9. Documentation and delivery evidence need final reconciliation

- README says the implementation “has not yet been scaffolded,” though the repository contains an extensive implementation.
- README still says the implementation “has not yet been scaffolded,” though the repository contains a substantial implementation.
- The current `MEMORY.md` baseline correctly records the mapped MCP endpoint and its remaining Sprint 12 work. Older Sprint 08 wording is historical and should not be used as current evidence.
- The non-integration suite passes as recorded below, while the unfiltered full suite remains blocked by finding 8. Historical sprint totals alone are not current release evidence.

## Intentional or acceptable variations

| Specification wording | Current implementation | Assessment |
| --- | --- | --- |
| Simple background email queue | Database outbox with idempotency/retries | Improvement for financial notifications. |
| No paid-record correction flow specified | Linked adjustment/reversal payments with approval | Improvement; protects paid history. |
| Example receipt route | `/collections/{id}/print` | Equivalent capability under a different route. |
| Audit-log name | `AuditRecord` | Neutral; structured target fields are the substantive variation. |
| System copy as CC/BCC | Separate outbox recipient/log | Delivery-equivalent but not header-equivalent. |

## Verification performed

```bash
dotnet test ElixomClaim.slnx --no-restore
```

The command **failed** in one relational Lib test. It emitted:

- a high-severity dependency vulnerability warning for `SSH.NET` `2025.1.0` (`GHSA-q939-rpr3-3284`) in the Lib test project;
- two non-blocking `NU1510` warnings for likely unnecessary options packages in `ElixomClaim.Lib`;
- an obsolete Testcontainers `MsSqlBuilder` constructor warning in `AuditRecordRelationalPersistenceTests`;
- a passing Web test project: **70 passed**;
- a Lib test project result of **117 passed, 1 failed**. The failure is `AuditRecords_RejectUpdatesAndDeletesAtTheSqlServerBoundary`, which cannot apply migrations to an empty SQL baseline because `dbclaim` is unavailable.

The current revision therefore does not have a passing full solution suite. The historical 181/182-test counts in sprint rows are not current full-suite evidence.

## Recommended completion order

1. Repair the empty-database migration/schema creation order and restore the relational audit test before claiming release verification.
2. Complete Sprint 12: wire the standard MCP transport and domain tools at `/mcp`, add the scope-separated `/api/v1` adapters, and retire legacy `/mcp/*` controller routes.
3. Correct collection-fee snapshotting/job calculation, then add regression tests for fee allocation and exact `TotalPaid` values.
4. Make production migrations safe across instances through a single-runner topology or database/distributed lock, with explicit production opt-in.
5. Resolve the SSH.NET advisory and unnecessary package references.
6. Complete structured audit fields, email-header logging if required, detailed payout print layout/subtotals, and universal `ILogger<T>` coverage.
7. Reconcile README, sprint records, and `MEMORY.md` with the actual MCP implementation and record reproducible fresh verification evidence.
