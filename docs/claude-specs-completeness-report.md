# Claude Specification Completeness Report

**Reassessed:** 2026-09-08

**Source:** [`context/claude-specs.md`](../context/claude-specs.md)

**Method:** Reviewed current production code, migrations, service/authorization boundaries, transport/API configuration, tests, and Sprint 12/MEMORY evidence. This report reflects the checked-out worktree rather than prior report conclusions.

## Conclusion

The implementation is substantially complete. Core domain workflows, the .NET 10 MVC/EF architecture, Google-only provisioned access, role hierarchy, durable outbox, custom OAuth, official MCP transport, versioned API, clean migration baseline, and privacy/UI requirements are all implemented and verified by the current suite.

It is **not yet release-ready** because several specification-fidelity and assurance items remain: payout documents are less detailed than required, audit/email schemas vary from the stated model, manager audit scope lacks a settled policy, universal logger injection is incomplete, and custom OAuth still requires formal external security/interoperability review.

## Verification

Executed successfully:

```bash
dotnet test ElixomClaim.slnx --no-restore --logger "console;verbosity=minimal"
```

| Project | Passed | Failed |
| --- | ---: | ---: |
| `ElixomClaim.Lib.Tests` | 129 | 0 |
| `ElixomClaim.Web.Tests` | 107 | 0 |
| Total | 236 | 0 |

The current run has no dependency-vulnerability, redundant-package, or obsolete-Testcontainers warnings.

## Requirement assessment

| Area | Status | Evidence / assessment |
| --- | --- | --- |
| .NET 10 MVC, Lib/Web/test structure | Implemented | Intended projects and responsibility split exist; no scaffold `Class1.cs` remains. |
| Azure SQL `dbclaim`, Guid IDs, money, UTC | Implemented | Guid technical IDs, display sequences, `decimal(18,2)`, JMD, UTC persistence, concurrency mappings, and clean Guid migration baseline are present. |
| Google SSO, provisioned users, bootstrap admin | Implemented | Google/cookie flow, active-user allow-list validation, no local password flow, and bootstrap seeding/promotion are wired. |
| Hierarchical roles and shared authorization | Implemented | Single role hierarchy, policies, active-user checks, and service ownership checks are implemented. |
| Claims | Mostly implemented | Draft/edit/submit, accept/reject, soft delete/default filtering, comments, payment state, dashboard, API, and MCP paths exist. Comment append-only behaviour is a service rule rather than a database invariant. |
| Collection clients and teller collection workflow | Mostly implemented | Admin configuration, assignments, client bank metadata, server-owned fee snapshots, custom entry snapshots, receipt queue/reissue, and HTML print are present. |
| Collection/job compatibility and settlement | Implemented | Collection status, same-client job constraint, Processing-only mutation, calculated fees/totals, settlement cascade, and idempotent notifications are service/database guarded. |
| Job payments and adjustment workflow | Mostly implemented | One-payee invariant, lifecycle, deductions, attachments, scheduling/payment, immutable paid records, and linked adjustments exist. Print/email detail is partial. |
| Salary/payroll | Implemented | Testable planner, inclusive recurrence bounds/tie-break, locked generated entries, custom-negative protection, submission job creation, and hosted daily scheduling are present. |
| SMTP/ACS, outbox, retry, email logs | Mostly implemented | Durable outbox, provider abstractions, retries, skipped-recipient handling, and per-attempt logs exist. Email log headers differ from the source model. |
| Custom OAuth server | Mostly implemented | Public PKCE-only registration, confidential-client secret validation, scope allow-lists, consent, code hashes, redirect validation, refresh rotation/family revocation, revoke, rate limiting, and audit are implemented. External review remains. |
| Official MCP transport and grouped tools | Implemented with residual coverage gap | Stateless SDK `/mcp` uses Bearer + `mcp:access` + rate limit; six tool classes are registered. Real HTTP tests cover discovery, invocation, actor isolation, missing/wrong scope, and legacy-route removal. Revoked/expired-token and cancellation protocol cases are not explicit. |
| MCP email preview/queue authorization | Implemented | Queueing delegates to shared services; preview service only allows approved templates, redacts data, and applies record-access rules: Telller-owned collection receipts, own-manager payouts, and broader Accountant/Administrator reconciliation access. API/MCP denial coverage exists. |
| Durable MCP/API operations | Implemented | Operations reserve actor-owned records before execution; status is owner-scoped. Outbox wake-ups are leased, processed by the hosted dispatcher, terminally updated, and stale leases are reclaimed after five minutes. Restart-recovery test coverage exists. |
| REST API and OpenAPI | Implemented with coverage gap | `/api/v1` exposes claims, collections, job payments, approved template preview/queue, payroll, and operation resources behind `api:access`; commands use idempotency. `/openapi/v1.json` is API-authenticated and excludes `/mcp`. Expand exhaustive endpoint/error contracts. |
| Audit trail/immutability | Mostly implemented | Audit trigger is relationally tested, mutations/security/MCP events are recorded, and audit payloads redact sensitive fields. Structured target fields differ from source model; manager audit scope needs a decision. |
| Privacy, frontend and print | Mostly implemented | Privacy policy/footer, Jamaica contact, CDN Bootstrap/jQuery with SRI, SVG favicon, semantic views, and HTML-only print output exist. Payout layout needs detail work. |
| `ILogger<T>` coverage | Mostly implemented | Most concrete controllers/services/workers/tools use structured logging. Remaining exceptions are listed below. |

## Resolved since the previous report

### Email-preview record access

[`ApprovedEmailPreviewService.cs`](../src/ElixomClaim.Lib/Services/ApprovedEmailPreviewService.cs) now applies record-level access before composing a preview:

- Teller-only collection receipt previews require `TellerUserId == actorUserId`.
- Manager payout previews require the manager to be the user payee.
- Accountant and Administrator retain wider settlement/reconciliation access.

The service returns no recipient/body data on denial, and API/MCP tests cover cross-actor denial and redaction. This resolves the prior broad-role-only preview finding.

### Durable outbox wake-up lifecycle

[`OutboxWakeUpProcessor.cs`](../src/ElixomClaim.Lib/Services/OutboxWakeUpProcessor.cs) is called by [`OutboxDispatchHostedService.cs`](../src/ElixomClaim.Web/HostedServices/OutboxDispatchHostedService.cs). It conditionally leases accepted/stale requests, dispatches the requested bounded batch, writes `Completed` or `Failed`, and reclaims a lease older than five minutes. The relational provider uses a conditional set-based claim; the in-memory path supports development/tests. Restart recovery has focused test coverage.

### OpenAPI and API contract surface

`Program.cs` now registers `AddOpenApi("v1")` and maps an `api:access`-protected `/openapi/v1.json`. HTTP tests confirm that it lists all approved API resources, excludes MCP, and denies unauthenticated callers. This resolves the former absent-OpenAPI finding.

## Remaining gaps and variations

### 1. Payout presentation and notification detail are partial

The job print page includes claims, collections with payer detail, payrolls/entries, deductions, adjustment context, and a headline calculation. It uses lists rather than the specified itemized tables/category subtotals.

The payout email composition includes claims, collections, deductions, and totals, but not linked payrolls/payroll entries. Complete both surfaces with tables, category subtotals, recipient/bank details, and regression tests that prove internal notes remain excluded.

### 2. Audit and email schemas are functional variations, not literal matches

[`AuditRecord.cs`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs) stores combined `Target` and `TimestampUtc`, instead of distinct `EntityType`, `EntityId`, and `OccurredAtUtc`. [`NotificationEntities.cs`](../src/ElixomClaim.Lib/Entities/NotificationEntities.cs) stores a single `Recipient`, not `To`/`From`/`Cc`/`Bcc` headers.

Separate system-copy messages are delivery-equivalent. Decide whether reporting/regulatory consumers require the literal model, then migrate deliberately if so.

### 3. Manager audit scope remains an explicit open decision

Managers can read the latest audit metadata while before/after state is withheld; Administrators see full audit state. The source specification explicitly asks whether Manager visibility is confined to claims/collections/job payments. The current query is not action-domain scoped. Record a decision and constrain the query if operational audit access should be limited.

### 4. Logging requirement is not literally complete

The following concrete types do not inject `ILogger<T>`:

- `ApprovedEmailPreviewService`
- `McpToolActorAccessor`
- `HomeController`
- `SalaryRecurrencePlanner`
- `SystemClock`

The planner/clock are pure utilities, but the source wording requires logging in every service. Either add safe structured outcome logs where meaningful or formally classify pure utilities as exempt.

### 5. Protocol/release assurance remains

The real MCP integration tests are now substantive, but they do not explicitly exercise revoked/expired token transport paths or cancellation. Add those cases if the selected SDK supports them.

The custom OAuth implementation still needs the specification's formal threat-model/interoperability/independent security review before production. Passing unit/integration tests do not replace that review.

### 6. Production migration topology still needs operational enforcement

The SQL Server `sp_getapplock` protects concurrent schema application when the production SQL Server path is used. Deployment should nevertheless designate one migration runner and ensure production startup passes the production flag; current automatic migration configuration should not be the only procedural control.

## Intentional/acceptable variations

| Specification wording | Current implementation | Assessment |
| --- | --- | --- |
| IDs may be int or Guid | Guid technical IDs plus durable display sequences | Improvement, consistently applied. |
| Simple email queue | Durable transactional outbox with retry/idempotency | Reliability improvement. |
| No correction workflow for paid records | Approval-based linked adjustments/reversals | Improvement preserving financial history. |
| Example `/mcp/sse` | Official stateless Streamable HTTP `/mcp` | Valid standards-conformant equivalent. |
| System copy as CC/BCC | Separate outbox recipient | Delivery-equivalent, not literal header capture. |

## Recommended completion order

1. Complete payout print/email tables, subtotals, linked payroll detail, and tests.
2. Resolve the structured audit/email header schema and Manager-audit-scope decisions.
3. Finish logger coverage or document the pure-utility exception.
4. Add explicit expired/revoked-token and cancellation MCP transport cases.
5. Enforce a production migration-runner procedure and obtain formal OAuth security/interoperability review.

## Delivery-state note

Sprint 12 remains in progress. This report is an assessment artifact and does not claim or complete any backlog item.
