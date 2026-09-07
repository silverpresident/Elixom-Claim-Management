# Claude Specification Completeness Report

**Re-evaluated:** 2026-09-07

**Source specification:** [`context/claude-specs.md`](../context/claude-specs.md)
**Method:** Source, migrations, routes, Razor views, tests, configuration, sprint ledger, and runtime wiring were reviewed. Findings are based on executable source rather than sprint-status assertions.

## Overall conclusion

The implementation is materially more complete than the 2026-09-03 review. Missing domain fields, profile management, manager/accountant workflows, audit trigger, development data, OAuth consent/lifetime work, rate limiting, and migration startup wiring have largely been added. Sprint 12 has also established the API/MCP transport contract, introduced the `api:access` scope, and added a shared authenticated-actor resolver.

It is still **not complete or release-ready**. The standard MCP transport is not registered or mapped, and the new actor resolver is not yet used by the running `/mcp/*` controllers. The application therefore continues to expose bespoke REST endpoints under `/mcp/*`. A second high-impact problem is incorrect collection-fee sourcing/allocation. Multi-instance migration locking, email/audit-schema fidelity, and the universal logging/layering requirements are also incomplete.

## Material change since the previous review

| Former gap | Current evidence | Re-evaluation |
| --- | --- | --- |
| Mixed `Guid`/`long` business IDs | Business entities and MVC routes now use `Guid`; see [`ClaimEntities.cs`](../src/ElixomClaim.Lib/Entities/ClaimEntities.cs). | Resolved for business-domain records. |
| Claim dates, user-bank data, collection fields, payout snapshots | New entities/mappings/forms/migration provide these fields. | Resolved / substantially resolved. |
| Job/payment and payroll UI gaps | [`JobPaymentsController.cs`](../src/ElixomClaim.Web/Controllers/JobPaymentsController.cs) and [`PayrollController.cs`](../src/ElixomClaim.Web/Controllers/PayrollController.cs) now expose core workflows. | Substantially resolved. |
| Mutable audit records | Migration [`20260903090000_AddAuditRecordAppendOnlyTrigger.cs`](../src/ElixomClaim.Lib/Migrations/20260903090000_AddAuditRecordAppendOnlyTrigger.cs) creates an Azure SQL UPDATE/DELETE trigger. | Resolved at the SQL boundary. |
| OAuth consent/lifetime/redirect/rate-limit gaps | OAuth service, consent persistence, strict redirect validation, and rate-limit configuration exist. | Substantially resolved. |
| Process-local MCP operation records | [`OperationRecordService.cs`](../src/ElixomClaim.Lib/Services/OperationRecordService.cs) persists idempotency records. | Resolved for durable records. |
| No transport-neutral actor boundary or separate REST scope | [`ActorResolver.cs`](../src/ElixomClaim.Web/Services/ActorResolver.cs), ADR 0005, and `api:access` OAuth scope are now present. | Partial remediation: no current `/mcp` or `/api/v1` endpoint consumes the boundary. |

## Requirement assessment

| Area | Status | Evidence / assessment |
| --- | --- | --- |
| .NET 10 MVC, Lib/Web/test split, EF Core, `dbclaim`, JMD precision | Implemented | Project structure, DbContext, migrations, and model/service tests support the baseline. |
| Google provisioned-user sign-in, bootstrap administrator, hierarchical roles | Implemented | Authentication configuration, validation, policies, and authorization tests support the intended model. |
| Claims lifecycle, ownership, comments, soft deletion, dates | Mostly implemented | [`ClaimService.cs`](../src/ElixomClaim.Lib/Services/ClaimService.cs), entities, MVC pages, and tests cover the workflow. Comment append-only behavior is not a database invariant. |
| Ordinary-user profile, bank details, payment history | Implemented | [`ProfileController.cs`](../src/ElixomClaim.Web/Controllers/ProfileController.cs), Razor view, masking helper, dashboard history, and migration `20260907090000_AddUserProfileDisplayAndBankFields` provide an optional display name plus bank branch-name/account-type fields. |
| Collection clients, options, capture, receipt/reissue/print | Mostly implemented | Admin/teller UI, client-scoped options, durable outbox, and HTML printing exist. Fee behavior is materially incorrect; see finding 2. |
| Job payments, deductions, lifecycle, settlement cascade, adjustment flow | Mostly implemented | Database constraint, shared service, MVC workflows, and lifecycle tests cover the core. Payout presentation remains incomplete. |
| Salary recurrence, generated payroll, adjustments/custom entries, submit-to-job | Implemented | Planner, service, hosted scheduler, MVC actions, and tests are present. |
| Durable email outbox, SMTP/ACS, retry, receipt/payout notifications | Mostly implemented | Outbox/sender implementations work, but the literal email-log/header schema is incomplete. |
| OAuth code + PKCE, rotation/revocation, consent, redirect validation, throttling, transport scopes | Mostly implemented | Core OAuth controls and both `mcp:access` / `api:access` scope definitions exist. Scope separation is not enforceable until the MCP/API transports are mapped. |
| Standard MCP server transport and registered-tool discovery | Missing | The package, contract, and actor boundary exist, but startup does not configure/map an MCP server and no standard MCP tools are registered. |
| Audit trail and append-only SQL enforcement | Mostly implemented | Audit service/redaction and the SQL trigger exist. The record shape differs from the specified EntityType/EntityId model. |
| Privacy, CDN frontend dependencies, favicon, HTML-only printing | Implemented | Layout, privacy page, the plum-and-gold SVG favicon, and assets conform. |
| Development-only samples and role switching | Implemented | Seeder and Development-only login route are guarded by environment/configuration. |

## Open differences and defects

### 1. Standard MCP transport is absent — high priority

Sprint 12 correctly identifies and plans the MCP transport defect. Its first two items are complete: ADR 0005 documents the `/mcp` versus `/api/v1` contract, `api:access` is now an OAuth scope, and [`ActorResolver.cs`](../src/ElixomClaim.Web/Services/ActorResolver.cs) centralizes active-user, scope, correlation, IP, and audit-context resolution. The actual runtime remains incomplete:

- [`Program.cs`](../src/ElixomClaim.Web/Program.cs) registers the actor resolver and tool classes as ordinary DI services only. It has no MCP-server registration or endpoint mapping, and does not expose `/api/v1`.
- No `MapMcp`, `McpServerTool`, `McpServerToolType`, or `IMcpActorResolver` implementation exists in source.
- Legacy REST controllers remain at `/mcp/claims`, `/mcp/collections`, `/mcp/email`, `/mcp/job-payments`, `/mcp/operations`, and `/mcp/payroll`; see [`McpClaimsController.cs`](../src/ElixomClaim.Web/Controllers/McpClaimsController.cs).

These custom bearer-authenticated APIs are not a discoverable/invocable standard MCP server, so a conforming MCP client cannot use the claimed transport. The scope contract is currently documentary/available to future adapters: the legacy controllers still perform their own actor resolution and string-split scope checks.

Related problems:

- `McpPayrollController` applies an Accountant policy but does not enforce the `mcp:access` scope required by other MCP REST controllers.
- Several tool classes query `ApplicationDbContext` directly instead of delegating to shared domain services, conflicting with the thin-adapter requirement.
- The MCP controllers/tools omit `ILogger<T>`.

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

### 8. Documentation and delivery evidence need final reconciliation

- README says the implementation “has not yet been scaffolded,” though the repository contains an extensive implementation.
- Historical Sprint 08 completion language and an older `MEMORY.md` decision-log entry claim standard MCP transport registration and REST-controller retirement. The current `MEMORY.md` baseline correctly supersedes that statement: Sprint 12 is in progress because the runtime still has neither.
- The current test command succeeded, but its visible output reported 65 passing Web tests and did not emit the ledger's claimed solution-wide 181-total summary. Do not present the historical total as fresh verification without preserving its original artifacts/log.

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

The command completed successfully. It emitted:

- a high-severity dependency vulnerability warning for `SSH.NET` `2025.1.0` (`GHSA-q939-rpr3-3284`) in the Lib test project;
- two non-blocking `NU1510` warnings for likely unnecessary options packages in `ElixomClaim.Lib`;
- an obsolete Testcontainers `MsSqlBuilder` constructor warning in `AuditRecordRelationalPersistenceTests`;
- a passing Web test segment with 65 tests.

The command's stdout did not provide a trustworthy solution-wide total for this run, so this report intentionally does not repeat the ledger's historical “181 tests” as current evidence.

## Recommended completion order

1. Complete Sprint 12: wire the standard MCP transport and domain tools at `/mcp`, add the scope-separated `/api/v1` adapters, and retire legacy `/mcp/*` controller routes.
2. Correct collection-fee snapshotting/job calculation, then add regression tests for fee allocation and exact `TotalPaid` values.
3. Make production migrations safe across instances through a single-runner topology or database/distributed lock, with explicit production opt-in.
4. Resolve the SSH.NET advisory and unnecessary package references.
5. Complete structured audit fields, email-header logging if required, detailed payout print layout/subtotals, and universal `ILogger<T>` coverage.
6. Reconcile README, sprint records, and `MEMORY.md` with the actual MCP implementation and record reproducible fresh verification evidence.
