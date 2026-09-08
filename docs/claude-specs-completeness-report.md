# Claude Specification Completeness Report

**Reassessed:** 2026-09-08

**Specification:** [`context/claude-specs.md`](../context/claude-specs.md)

**Method:** Reviewed the current source, migrations, routes, service boundaries, tests, configuration, `MEMORY.md`, and Sprint 12 ledger. Findings are based on the worktree, not historical sprint assertions alone.

## Conclusion

The core application is substantially implemented: .NET 10 MVC/EF architecture, Google-only provisioned-user access, hierarchical roles, claims, collections, job payments, payroll, SMTP/ACS outbox delivery, custom OAuth, standard MCP, audit persistence, and the HTML-first UI are present.

The former clean-migration, MCP-operation durability, direct MCP email-queue mutation, and missing REST-API findings are materially resolved. The system remains **not release-ready or fully specification-complete** because it lacks endpoint-level API/MCP integration tests, MCP previews still bypass shared authorization-aware services, some API commands lack the mandated idempotency/durable-operation model, production migration coordination is process-local, and formal OAuth/MCP security review remains open.

## Verification

The focused non-relational command completed successfully:

```bash
dotnet test ElixomClaim.slnx --no-restore --filter "Category!=Integration" --logger "console;verbosity=minimal"
```

Results: **125 Lib tests passed** and **78 Web tests passed** (203 total, no failures).

An unfiltered test run was also initiated, but its relational/Testcontainers portion did not finish within the tool window. The prior full-suite evidence in the repository should be rerun and recorded after the recent API/OAuth changes. Current warnings include high-severity advisory `GHSA-q939-rpr3-3284` for `SSH.NET` 2025.1.0, two Lib `NU1510` warnings, and obsolete Testcontainers builder use in the relational audit test.

## Requirement assessment

| Specification area | Status | Evidence / assessment |
| --- | --- | --- |
| .NET 10 MVC, Lib/Web/tests, EF Core, Azure SQL `dbclaim` | Implemented | Four intended projects and the expected layering exist. The migration baseline is Guid-based and clean-SQL relational coverage is recorded. |
| Guid IDs, JMD precision, UTC and display record numbers | Implemented | Current entities/mappings use Guid technical IDs, `decimal(18,2)`, persisted UTC values, and sequence-backed display numbers. |
| Google SSO, provisioned active users, bootstrap administrator | Implemented | Google/cookie wiring, active-user validation, and bootstrap admin seed/promotion are present. |
| Hierarchical roles, policy and ownership checks | Mostly implemented | Single-role hierarchy and shared policies are present. The MCP preview exception below prevents a complete result. |
| Claims lifecycle, comments, soft deletion | Mostly implemented | Services/MVC/API cover the principal lifecycle. Comment append-only behaviour is a service convention, not a database invariant. |
| Profile, bank details and payment history | Implemented | Optional display name, constrained bank metadata, masking, and user payment history are present. |
| Clearing-house clients, capture, receipt/reissue/print | Mostly implemented | Client configuration, fee snapshots, custom purpose/amount snapshots, capture, outbox receipts, reissue and HTML print are implemented. |
| Job payments, deductions, lifecycle and settlement cascade | Mostly implemented | One-payee invariant, Processing-only mutation, total calculation, cascade/outbox settlement, and adjustment workflow are implemented. Payout document fidelity remains partial. |
| Salary recurrence, payroll generation/entries/submission | Implemented | Shared planner/service, hosted scheduler, locked generated entries, custom-entry validation, and payroll-bound job payment are present. |
| SMTP/ACS, durable outbox, retry and email logging | Mostly implemented | Sender implementations, outbox worker, retries, skipped-recipient handling, and logs exist. Literal email header-schema fidelity differs. |
| In-house OAuth code flow, PKCE S256, consent, refresh/revocation | Mostly implemented | Registration now creates public PKCE-only clients without a secret; allowed scopes are persisted/validated, and confidential clients must present stored credentials. Formal security/interoperability review remains open. |
| Standard MCP transport and grouped tools | Mostly implemented | Official Streamable HTTP `/mcp` is bearer-authenticated, scope-gated/rate-limited, and exposes six grouped tool classes. Protocol-level integration evidence is still absent. |
| Durable MCP operations | Mostly implemented | Actor-scoped records are reserved before salary work; retries return the reservation; status is actor-scoped; MCP does not invoke outbox dispatch. Restart recovery still lacks end-to-end evidence. |
| Versioned REST API and `api:access` isolation | Mostly implemented | `/api/v1` now has claims, collections, job-payment, email queue, payroll, and operation-status endpoints behind `api:access`, using actor resolution and mostly shared services. Command/idempotency and API-contract gaps remain. |
| Append-only audit trail | Mostly implemented | SQL trigger migration and relational evidence exist. The model combines type/ID as `Target`, unlike the specified separate fields. |
| Privacy, CDN assets, SVG favicon, HTML-only printing | Implemented | Privacy/footer, CDN Bootstrap/jQuery with SRI, custom favicon, and HTML print routes exist; no scaffold `Class1.cs` or local frontend distribution was found. |
| `ILogger<T>` in all controllers/services/hosted services | Mostly implemented | Recent changes add structured logs across controllers/tools. `HomeController`, `McpToolActorAccessor`, and pure utility services (`SalaryRecurrencePlanner`, `SystemClock`) still lack a logger. |

## Resolved since the previous assessment

### Clean migration baseline and relational evidence

[`20260908033036_InitialCreate.cs`](../src/ElixomClaim.Lib/Migrations/20260908033036_InitialCreate.cs) creates the `dbclaim` schema, display-number sequences, and Guid-based tables in dependency order. [`20260908033045_AddAuditRecordAppendOnlyTrigger.cs`](../src/ElixomClaim.Lib/Migrations/20260908033045_AddAuditRecordAppendOnlyTrigger.cs) restores the database-level audit trigger. The reset is documented as safe only because no deployed data exists.

### MCP durable operation and queue boundary

[`OperationRecordService.cs`](../src/ElixomClaim.Lib/Services/OperationRecordService.cs) reserves an actor-scoped `Accepted` record before salary execution and filters status by actor. [`OperationsTools.cs`](../src/ElixomClaim.Web/Mcp/Tools/OperationsTools.cs) no longer invokes outbox dispatch directly.

Production email queue requests from [`EmailTools.cs`](../src/ElixomClaim.Web/Mcp/Tools/EmailTools.cs) now delegate to `ICollectionService.QueueReceiptAsync` and `IJobPaymentService.QueuePaymentSummaryAsync`. Those shared services own authorization, recipient selection, idempotency, outbox persistence, and audit behaviour. This resolves the prior direct adapter mutation finding for queueing.

### API resource coverage and OAuth client admission

The API now exposes the approved resource categories:

- `ClaimsApiController`: list, detail, draft submission;
- `CollectionsApiController`: permitted list/detail;
- `JobPaymentsApiController`: permitted list/detail;
- `EmailTemplatesApiController`: constrained approved-template queue only;
- `PayrollApiController`: preview/run; and
- `OperationsApiController`: actor-owned operation status.

Each is gated by `api:access`. Dynamic OAuth registration/client scope admission is also materially stronger than in the prior review.

## Remaining material gaps

### 1. API command/idempotency and integration contract coverage — high priority

The new API controllers have no discovered API-specific integration/contract test class. The current test inventory contains MCP boundary/security tests but no API endpoint tests. Add tests using the in-process host for bearer authentication, `api:access` isolation, ownership/role boundaries, pagination limits, malformed input, Problem Details, sensitive projections, and each endpoint's success/failure contract.

The Sprint 12 contract also requires idempotency for commands. `POST /api/v1/payroll/run` executes `GenerateForDefinitionAsync` directly and accepts no idempotency key; it therefore does not offer the durable operation/idempotency semantics of MCP salary generation. The operations API offers only `GET` status, not an approved operation-request command. Either route payroll run through the durable operation service with an idempotency key, or document/prove the database uniqueness behaviour as the API's complete duplicate-request contract.

### 2. MCP email previews still bypass shared authorization-aware services — high priority

`EmailTools` correctly delegates **queueing**, but `PreviewCollectionReceiptAsync` and `PreviewPaymentSummaryAsync` still directly query `ApplicationDbContext`. Collection preview checks only Teller-or-higher and does not apply the recording-teller-or-manager restriction enforced by [`CollectionService.ReissueReceiptAsync`](../src/ElixomClaim.Lib/Services/CollectionService.cs). A Teller can potentially preview another teller's record by ID.

Move previews and their redacted recipient/template projections into shared Lib services, use the same actor ownership/client access decisions as MVC, and test cross-user denial plus role-sensitive bank/message redaction.

### 3. MCP protocol and operation restart recovery are not proved

The code registers standard Streamable HTTP MCP and contract tests inspect tool metadata, but no in-process conforming MCP-client tests were found for initialize, discovery, invocation, cancellation, missing/wrong/revoked/expired bearer token, scope denial, and transport error behaviour.

Likewise, an `Accepted` durable operation is observable, but there is no demonstrated process-interruption/restart policy that completes, fails, or safely retries it. Define and test that recovery contract.

### 4. Migration coordination is process-local

[`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) uses a static `SemaphoreSlim`; it cannot coordinate two application instances or independent deployment jobs. The specification requires one migration runner/instance. Use an explicit single-runner deployment topology or database/distributed lock.

### 5. Payout output and audit/email schema fidelity are partial

The job print view includes collection payer details, claims, payroll entries, deductions, and adjustment context, but still uses lists rather than the specified itemized tables/subtotals. The email payout composition does not include linked payroll entries.

[`AuditRecord.cs`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs) stores a combined `Target`, not separate entity type/ID. [`NotificationEntities.cs`](../src/ElixomClaim.Lib/Entities/NotificationEntities.cs) stores one `Recipient`, not literal `To`/`From`/`Cc`/`Bcc` headers. Separate system-copy messages are delivery-equivalent, but not a literal schema match.

### 6. Logging and release hardening remain

Add/redact structured logging to `HomeController` and `McpToolActorAccessor`; decide whether the literal requirement includes pure utility services. Resolve `SSH.NET` advisory `GHSA-q939-rpr3-3284` and the Testcontainers obsolete API warning.

The custom OAuth server still requires the formally stated threat-model/interoperability/independent security review before production release.

### 7. Documentation state conflicts

`README.md` still states that implementation "has not yet been scaffolded," which conflicts with the implemented system. `MEMORY.md` currently says Sprint 12 item 6 is blocked pending a decision to retain/de-scope the API, while a later current-baseline entry says product direction is to retain and complete it. Reconcile these records before the next handoff; the API code itself is present and partly complete.

## Intentional or acceptable variations

| Specification wording | Current implementation | Assessment |
| --- | --- | --- |
| IDs may be int or Guid | Guid technical IDs plus durable display sequences | Improvement and consistently applied. |
| Simple background email queue | Durable outbox with idempotency/retries | Reliability improvement. |
| No correction path stated for paid records | Linked approval-based adjustments/reversals | Improvement preserving financial history. |
| Example `/mcp/sse` endpoint | Official stateless Streamable HTTP `/mcp` | Valid modern equivalent. |
| System copy as CC/BCC | Separate recipient/outbox record | Delivery-equivalent, not literal header logging. |
| `AuditLogEntry` naming | `AuditRecord` | Neutral naming; target-field structure is the material variation. |

## Recommended completion order

1. Add API integration/contract tests and complete command idempotency/durable-operation semantics.
2. Move MCP previews behind shared authorization-aware Lib services; prove ownership/redaction boundaries.
3. Add real MCP protocol lifecycle/security/cancellation tests and durable-operation restart-recovery tests.
4. Make migrations multi-instance safe and reconcile README/MEMORY/Sprint 12 status.
5. Finish payout presentation/schema-fidelity work as required, eliminate warnings/vulnerable dependency, and complete OAuth security review.

## Delivery-ledger note

This document is an assessment artifact only. It does not claim or complete Sprint 12 work.
