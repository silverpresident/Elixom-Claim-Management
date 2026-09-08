# Claude Specification Completeness Report

**Reassessed:** 2026-09-08

**Specification:** [`context/claude-specs.md`](../context/claude-specs.md)

**Method:** Reviewed current source, migration ledger, configuration, routes, Razor, tests, and Sprint 12 evidence. Findings reflect the checked-out worktree, not historic sprint claims alone.

## Conclusion

The implementation is substantially complete across the core product: the Lib/Web split, Google-only provisioned-user access, hierarchical roles, claims, clearing-house collections, job payments, payroll, outbox delivery, custom OAuth, standard MCP transport, and HTML-first UI are all present.

It is still **not release-ready or specification-complete**. The migration and core MCP-operation defects reported in the earlier review are resolved, but the versioned REST API is only a claims slice, MCP adapters still bypass shared service boundaries in sensitive areas, formal MCP/OAuth interoperability evidence is absent, and several specification-fidelity/security-quality gaps remain.

## Current verification

```bash
dotnet test ElixomClaim.slnx --no-restore --logger "console;verbosity=minimal"
```

Passed on 2026-09-08:

- Lib: **123 passed**
- Web: **73 passed**
- Total: **196 passed, 0 failed**

The run includes the relational/Testcontainers tests. Warnings remain: `SSH.NET` 2025.1.0 has high-severity advisory `GHSA-q939-rpr3-3284`; the Lib project has two `NU1510` package-reference warnings.

## Requirement assessment

| Specification area | Status | Current evidence / variation |
| --- | --- | --- |
| .NET 10 MVC, Lib/Web/test split, EF Core, Azure SQL `dbclaim` | Implemented | Four projects exist with the intended ownership split. The migration ledger was reset to a clean Guid baseline because `MEMORY.md` records that no deployed database/data required preservation. |
| Guid identifiers, JMD precision, UTC timestamps, display record numbers | Implemented | Current entities/mappings use Guid technical IDs, `decimal(18,2)`, UTC fields, and durable sequence-backed display numbers. |
| Google SSO, provisioned active users, bootstrap administrator | Implemented | Google/cookie wiring, active-user validation, and bootstrap seeding/promoting are present. |
| Hierarchical roles and policy/ownership checks | Mostly implemented | Single-role hierarchy and policies are in place. Some MCP email paths do not carry the same ownership checks as their MVC/shared-service equivalents. |
| Claims lifecycle, ownership, comments, soft delete | Mostly implemented | Services, MVC, and the new API slice cover the principal lifecycle. Append-only comments remain a service convention rather than database enforcement. |
| Profile, bank details and payment history | Implemented | Optional display name, bank branch/account-type validation, masking, and payment history are present. |
| Collection client administration and teller clearing house | Mostly implemented | Client/options/bank configuration, fee snapshots, custom-entry snapshots, capture, reissue, outbox receipt, and print HTML exist. |
| Job-payment lifecycle and paid cascade | Mostly implemented | One-payee invariant, Processing-only mutation, totals, settlement cascade, idempotent notification, and adjustment workflow are implemented. Payout document fidelity remains partial. |
| Salary recurrence, generated payroll, entries and submission | Implemented | Planner, service, scheduler, locked generated entries, custom-entry rule, and payroll-bound job payment exist. |
| SMTP/ACS, durable outbox, retry, email logging | Mostly implemented | Senders, durable outbox, worker, retries, skipped-recipient handling, and logs exist. The literal email-header/audit shape differs from the source specification. |
| In-house OAuth code flow, PKCE S256, refresh/rotation, revocation, consent | Mostly implemented | The server and protections are implemented; formal interoperability and independent security review are still release requirements. |
| Standard MCP transport and six grouped tools | Mostly implemented | Official Streamable HTTP is mapped at `/mcp`, bearer-authenticated, scope-gated, rate-limited, and exposes six domain classes/14 tools. Shared-service-boundary and protocol-test gaps remain. |
| Durable, actor-owned MCP operations | Mostly implemented | Operation records are reserved as `Accepted` before salary work, duplicate calls return the existing record, status lookup is actor-scoped, and MCP no longer dispatches outbox work directly. Restart recovery semantics still need end-to-end proof. |
| Versioned REST API with `api:access` | Partial | `/api/v1/claims` has list/detail/submit, bounded paging, actor resolution, shared claim service, and isolated scope. The required collections, job payments, constrained email, payroll, and operation resources are absent. |
| Append-only audit trail | Mostly implemented | SQL trigger migration and relational test evidence now exist. The record's single `Target` remains less structured than the specified entity-type/entity-ID fields. |
| Privacy, CDN-only frontend, SVG favicon, HTML-only printing | Implemented | Privacy policy/footer, CDN Bootstrap/jQuery with SRI, custom SVG favicon, and HTML print routes are present; no `Class1.cs` or local frontend distribution was found. |
| `ILogger<T>` in all controllers/services/hosted services | Partial | Hosted services and many services/adapters log, but several required concrete types still omit a logger. |

## Resolved since the prior report

### Clean SQL migration baseline

The broken historical migration chain has been replaced with [`20260908033036_InitialCreate.cs`](../src/ElixomClaim.Lib/Migrations/20260908033036_InitialCreate.cs), which creates `dbclaim`, display-number sequences, and Guid-based tables in dependency order. [`20260908033045_AddAuditRecordAppendOnlyTrigger.cs`](../src/ElixomClaim.Lib/Migrations/20260908033045_AddAuditRecordAppendOnlyTrigger.cs) restores SQL-side audit immutability. Full tests now pass, including relational coverage.

This is an appropriate correction only under the documented no-deployed-data assumption. If a database is later deployed, future schema evolution must use data-preserving migrations rather than another baseline reset.

### MCP operation durability and ownership

[`OperationRecordService.cs`](../src/ElixomClaim.Lib/Services/OperationRecordService.cs) now reserves a unique actor-scoped record before execution, and [`OperationsTools.cs`](../src/ElixomClaim.Web/Mcp/Tools/OperationsTools.cs) returns that reservation on duplicate calls. Operation status reads filter by actor. The outbox tool records an approved wake-up request rather than calling `DispatchDueAsync` itself.

This resolves the earlier pre-side-effect persistence, cross-actor status disclosure, and direct-dispatch findings. The remaining concern is evidence: add restart/concurrent-recovery integration tests that prove an `Accepted` record reaches a determinate safe state after a process interruption.

### First isolated REST API slice

[`ClaimsApiController.cs`](../src/ElixomClaim.Web/Controllers/Api/ClaimsApiController.cs) supplies `api:access`-protected claims list, detail, and draft-submission routes with actor resolution, ownership through `IClaimService`, limits of 1–100 page items, and audit for submission. This supersedes the prior finding that no REST API existed.

## Remaining material gaps

### 1. REST API is incomplete — high priority

Sprint 12 requires a useful `/api/v1` replacement, not merely a claims endpoint. No API controllers/resources were found for:

- permitted collection list/detail;
- permitted job-payment list/detail;
- constrained template preview/queue;
- authorized payroll preview/run; and
- durable operation request/status.

The API also has no discovered integration/OpenAPI contract coverage. Complete the resource set with protocol-specific DTOs, Problem Details validation, ownership/sensitive projections, idempotency for commands, and `api:access` separation tests.

### 2. MCP email adapters bypass the shared authorization/service boundary — high priority

[`EmailTools.cs`](../src/ElixomClaim.Web/Mcp/Tools/EmailTools.cs) directly queries `ApplicationDbContext`, composes templates, and adds `EmailOutboxItem` records. Its collection preview/queue checks Teller-or-higher but does not apply the recording-teller-or-manager constraint enforced by [`CollectionService.ReissueReceiptAsync`](../src/ElixomClaim.Lib/Services/CollectionService.cs). A Teller can therefore request a collection by arbitrary ID through MCP more broadly than through the corresponding shared operation.

This violates the requirement that MCP invokes the same authorization-aware Lib services as MVC rather than recreating decisions in an adapter. Move approved preview/queue operations and safe projections into shared Lib services, enforce client/record ownership, and add cross-user/role/redaction tests.

### 3. MCP protocol and operation-recovery verification is incomplete

The tool-contract/boundary tests demonstrate attributed tool definitions and some adapter behaviour. No in-process conforming MCP client test was found for protocol initialization, discovery, invocation, cancellation, missing/wrong/revoked/expired bearer token, scope denial, and transport error handling.

The new `Accepted` operation status also needs restart and concurrent-worker recovery tests. A record that remains indefinitely `Accepted` after an interruption is observable but not a complete retry/recovery contract.

### 4. Migration execution is still only process-local

[`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) uses a static `SemaphoreSlim`; this cannot serialize migrations across two application instances or deployment jobs. The specification requires a single migration runner/instance. Use a controlled deployment topology or a database/distributed lock, and require explicit production migration authority.

### 5. Audit and email-log structure differs from the stated model

- [`AuditRecord.cs`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs) combines `EntityType` and `EntityId` into `Target`, reducing query/reporting structure compared with the specification.
- [`NotificationEntities.cs`](../src/ElixomClaim.Lib/Entities/NotificationEntities.cs) records one `Recipient` rather than the specified `To`, `From`, `Cc`, and `Bcc` fields. Separate system-copy outbox records are delivery-equivalent, but not header-equivalent.

These are design variations, not evidence of failed delivery. Resolve them only if literal schema fidelity or downstream reporting requires it.

### 6. Payout summary content is partial

The printable job-payment view now includes payer name/email for collections, claims, payroll entries, deductions, and adjustment context. It still uses lists rather than the requested itemized tables with category subtotals. The email composer does not include linked payrolls/entries, and both surfaces need a more explicit recipient/bank/totals/itemization presentation to fully meet the specification.

### 7. Required logging coverage remains incomplete

Concrete types still lacking `ILogger<T>` include:

- Controllers: `AdminController`, `ClaimsController`, `HomeController`, `JobPaymentsController`, and `ManagerClaimsController`.
- Services: `SalaryRecurrencePlanner` and `SystemClock` (pure utilities, but the specification states every service).
- MCP tool adapters and `McpToolActorAccessor`.

Add structured, redacted logs without recording account numbers, message bodies, secrets, or access tokens.

### 8. Release/security hardening remains open

The project still needs the formally required OAuth threat-model/interoperability/independent security review. The current full test run also flags high-severity advisory `GHSA-q939-rpr3-3284` for `SSH.NET` 2025.1.0. Resolve or replace the vulnerable dependency before release.

## Intentional or acceptable variations

| Specification wording | Current implementation | Assessment |
| --- | --- | --- |
| IDs may be int or Guid | Guid technical IDs plus durable display sequences | Improvement; consistently applied. |
| Simple background email queue | Durable outbox with idempotency/retries | Reliability improvement for financial notifications. |
| No correction workflow specified for paid records | Linked approval-based adjustment/reversal payments | Improvement that preserves immutability. |
| Example MCP endpoint `/mcp/sse` | Official stateless Streamable HTTP `/mcp` | Valid modern equivalent. |
| System copy as CC/BCC | Separate recipient/outbox record | Delivery-equivalent but not literal header logging. |
| `AuditLogEntry` naming | `AuditRecord` | Neutral naming variation; structured target fields are the material difference. |

## Recommended completion order

1. Finish Sprint 12 item 6: complete `/api/v1` resources and their scope, ownership, pagination/idempotency, Problem Details, and contract tests.
2. Refactor MCP email operations behind shared authorization-aware Lib services; add cross-user/role/redaction integration coverage.
3. Add real MCP protocol lifecycle/security/cancellation tests and operation restart-recovery tests.
4. Make production migration execution safe across instances and explicit in deployment configuration.
5. Close logging/payout/document-schema fidelity gaps as required, resolve the vulnerable dependency, and complete OAuth security/interoperability review.

## Delivery-ledger note

`MEMORY.md` records Sprint 12 item 6 as active. This report is an assessment artifact only; it does not claim a backlog item or alter sprint completion state.
