# Claude Specification Completeness Report

**Reassessed:** 2026-09-08

**Specification:** [`context/claude-specs.md`](../context/claude-specs.md)

**Method:** Reviewed the current source, migrations, routes, service boundaries, tests, configuration, `MEMORY.md`, and Sprint 12 ledger. Findings are based on the worktree, not historical sprint assertions alone.

## Conclusion

The core application is substantially implemented: .NET 10 MVC/EF architecture, Google-only provisioned-user access, hierarchical roles, claims, collections, job payments, payroll, SMTP/ACS outbox delivery, custom OAuth, standard MCP, audit persistence, and the HTML-first UI are present.

The former clean-migration, MCP-operation durability, direct MCP email-queue mutation, MCP preview service-boundary, API command-idempotency, and migration-coordination findings are materially resolved. The system remains **not release-ready or fully specification-complete** because full endpoint-level API contract coverage and real MCP transport integration tests are incomplete, and formal OAuth/MCP security review remains open.

## Verification

The focused non-relational command completed successfully:

```bash
dotnet test ElixomClaim.slnx --no-restore --filter "Category!=Integration" --logger "console;verbosity=minimal"
```

Results: this historical command predated the current full-suite verification.

The current unfiltered suite passes after the API/OAuth changes: **126 Lib tests and 92 Web tests (218 total)**. The transitive SSH.NET advisory, redundant Options package warnings, and obsolete Testcontainers builder usage are resolved.

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

### 1. API contract coverage remains incomplete

`ApiEndpointIntegrationTests` now covers bearer/scope isolation, ownership and role boundaries, pagination failures, sensitive collection/email projections, approved operation requests/status, and claim/payroll idempotent replay. The remaining work is exhaustive endpoint-level success/failure and Problem Details contract coverage, especially approved email-queue success and all list/detail validation combinations.

### 2. MCP email preview boundary is resolved

MCP and REST previews delegate to the Lib-owned `IApprovedEmailPreviewService`, which enforces role authorization and returns only approved redacted template content/recipient summaries. Real HTTP coverage confirms collection previews exclude payor name/telephone and mask recipient email. The remaining concern is protocol-level MCP invocation evidence, not an adapter service-boundary bypass.

### 3. MCP protocol and operation restart recovery are not proved

The code registers standard Streamable HTTP MCP and contract tests inspect tool metadata, but no in-process conforming MCP-client tests were found for initialize, discovery, invocation, cancellation, missing/wrong/revoked/expired bearer token, scope denial, and transport error behaviour.

Likewise, an `Accepted` durable operation is observable, but there is no demonstrated process-interruption/restart policy that completes, fails, or safely retries it. Define and test that recovery contract.

### 4. Migration coordination is resolved in production SQL Server

[`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) retains its local guard and additionally uses SQL Server's session-scoped `sp_getapplock` across the pending-migration check and migration run when production uses the SQL Server provider. Deployment should still designate a migration runner operationally, but concurrent application instances no longer race schema application.

### 5. Payout output and audit/email schema fidelity are partial

The job print view includes collection payer details, claims, payroll entries, deductions, and adjustment context, but still uses lists rather than the specified itemized tables/subtotals. The email payout composition does not include linked payroll entries.

[`AuditRecord.cs`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs) stores a combined `Target`, not separate entity type/ID. [`NotificationEntities.cs`](../src/ElixomClaim.Lib/Entities/NotificationEntities.cs) stores one `Recipient`, not literal `To`/`From`/`Cc`/`Bcc` headers. Separate system-copy messages are delivery-equivalent, but not a literal schema match.

### 6. Release hardening remains

The SSH.NET advisory is resolved and redundant Options/Testcontainers warnings have been removed. The custom OAuth server still requires the formally stated threat-model/interoperability/independent security review before production release.

### 7. Documentation state is reconciled

README, MEMORY, and the Sprint 12 ledger record that the implementation is active, the versioned API is retained, and Sprint 12 remains in progress solely for remaining API/MCP evidence and external review.

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
