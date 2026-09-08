# Claude Specification Completeness Report

**Thorough reassessment:** 2026-09-08

**Source specification:** [`context/claude-specs.md`](../context/claude-specs.md)

**Review method:** Inspected production source, entities/mappings/migrations, MVC and API/MCP adapters, authorization paths, hosted services, Razor output, configuration, test inventory, current Sprint 12 ledger, and executed the full solution suite. Findings distinguish implementation evidence from test evidence and specification variations from defects.

## Executive conclusion

The product is substantially implemented. Its expected .NET 10 MVC/EF architecture; Google-only provisioned-user access; hierarchical roles; claims, collection, job-payment, and payroll workflows; durable notifications; in-house OAuth; standard MCP transport; append-only audit trigger; versioned API; and accessible HTML-first UI are all present.

It is **not yet release-ready**. Two concrete authorization/operation gaps remain:

1. Approved email **previews** enforce a minimum role but not ownership/record access. A Teller can preview a different teller's collection if they know its ID; a Manager can preview any job payment. This is contrary to the shared authorization and least-privilege requirements.
2. An approved outbox-wake-up operation is stored/audited but has no worker consumer or completion transition. It remains `Accepted` while the dispatcher continues its ordinary 30-second polling; it is therefore not an observable, completed wake-up command.

The main remaining delivery work is to close those boundaries, add explicit API OpenAPI/contract coverage, define durable-operation recovery, complete a few schema/presentation fidelity items, and complete the required OAuth security review.

## Verification executed

```bash
dotnet test ElixomClaim.slnx --no-restore --logger "console;verbosity=minimal"
```

Passed on this reassessment:

| Project | Passed | Failed |
| --- | ---: | ---: |
| `ElixomClaim.Lib.Tests` | 126 | 0 |
| `ElixomClaim.Web.Tests` | 98 | 0 |
| Total | 224 | 0 |

The run completed cleanly without the prior SSH.NET vulnerability, redundant Options references, or obsolete Testcontainers warning. Passing tests are strong evidence but do not remove the untested ownership/recovery risks identified below.

## Requirement-by-requirement assessment

| Specification area | Status | Evidence and assessment |
| --- | --- | --- |
| Solution structure and target stack | Implemented | `ElixomClaim.Lib`, `ElixomClaim.Web`, and matching xUnit projects exist. Business services/EF entities are in Lib; MVC, OAuth, MCP, and hosted adapters are in Web. No `Class1.cs` exists. |
| Database schema, identifiers, money, timestamps | Implemented | `dbclaim`, Guid technical keys, `decimal(18,2)`, JMD, UTC fields, concurrency tokens, and database-backed display sequence numbers are mapped. The reset Guid baseline is documented as valid because no deployed database/data exists. |
| Clean migration and audit immutability | Implemented with operational caveat | Current baseline migration creates dependency-ordered objects; relational tests pass. SQL trigger `TR_AuditRecords_PreventMutation` prevents audit updates/deletes. Production locking uses SQL Server `sp_getapplock`, although deployment still needs a designated migration authority. |
| Google SSO and bootstrap administrator | Implemented | Cookie + Google flow, pre-provisioned active-user validation, a not-provisioned path, and configured bootstrap-admin seed/promotion are present. No local password flow was found. |
| Role model and policies | Implemented | One hierarchical role uses `Blocked` through `Administrator`; policies/role extensions support inheritance. Service checks are generally the primary authorization boundary. |
| Claims | Mostly implemented | Draft create/edit/submit, management accept/reject, ownership, soft deletion/default filtering, append-only comments, payment state, dashboard history, MVC, API, and MCP support are present. Database-level comment immutability is not enforced. |
| Collection clients and teller collections | Mostly implemented | Administrator-managed client/options/assignments/bank details; teller capture; server-owned fee snapshots; custom purpose/amount snapshots; local-time conversion; receipt queue/reissue; and HTML print are implemented. |
| Collection lifecycle and job compatibility | Implemented | Service validation restricts attachment to `Collected` records, requires a single compatible collection client per job, snapshots processing fees, and settlement transfers collections. |
| Job payment lifecycle | Mostly implemented | Database/service one-payee invariant; Processing-only line changes; manager submission; accountant scheduling/settlement; atomic cascade to claims/collections/payrolls; adjustment/reversal path; and idempotent notification keys are present. Presentation fidelity remains partial. |
| Salary and payroll | Implemented | Pure recurrence planner, inclusive bounds/tie-break, generated locked base/benefit/deduction entries, custom-negative-net-pay protection, submission lock, generated job payment, and daily hosted scheduling are present. |
| Email transports/outbox | Mostly implemented | Lib supplies SMTP and ACS sender implementations, durable outbox, retry/backoff, invalid-payor skip, per-attempt logs, and hosted dispatch. The `EmailLog` field model is not a literal `To`/`From`/`Cc`/`Bcc` match. |
| OAuth authorization server | Mostly implemented | Dynamic public-client registration is PKCE-only/no secret; registered scope allow-list is checked; confidential clients require secret; authorization code hashes, strict redirects, consent, rotation/replay-family revocation, revocation, rate limiting, and audit exist. Independent security/interoperability review remains a release condition. |
| Standard MCP transport | Implemented with coverage gaps | Official stateless Streamable HTTP maps only `/mcp`, gated by Bearer + `mcp:access` + rate limit. Six grouped tool classes expose stable tools. Real HTTP tests cover discovery, invocation, missing/wrong scope, actor isolation, and legacy route retirement. Cancellation/revoked-or-expired-token protocol scenarios are not explicitly covered. |
| MCP tool service boundary | Mostly implemented | Claim/collection/job reads and production email queues delegate to Lib services; tools resolve the concrete actor and audit MCP activity. Preview access remains too broad; see finding 1. |
| Durable operations | Partial | Actor-scoped reservation precedes salary operation and duplicate requests return the same record. Status reads are actor-scoped. Outbox wake-up never advances beyond accepted; restart recovery is undefined; see finding 2. |
| REST API | Mostly implemented | `/api/v1` has claims, collections, job payments, approved email preview/queue, payroll preview/run, and operation request/status endpoints behind separate `api:access`. HTTP tests cover scope isolation, key ownership/role/redaction paths, and several idempotency flows. No OpenAPI document/generation or formal full contract suite was found. |
| Audit logging and visibility | Mostly implemented | Mutations, OAuth, operations, and MCP adapters audit; redaction protects common secrets/bank fields. Managers can query the latest 200 audit metadata records without action-domain scoping, which leaves the spec's stated manager-scope ambiguity unresolved. |
| Privacy and frontend | Implemented | Real Jamaica-oriented privacy policy/footer, privacy contact, CDN Bootstrap/jQuery with SRI, deliberate SVG favicon, semantic Razor forms/status components, responsive/print CSS, and no PDF library/generation were found. |
| Required structured logging | Mostly implemented | Most controllers, services, hosted services, and tool classes now log redacted identifiers/outcomes. `HomeController`, `McpToolActorAccessor`, `ApprovedEmailPreviewService`, `SalaryRecurrencePlanner`, and `SystemClock` still do not inject `ILogger<T>`. The latter two are pure utilities, but the source specification uses a literal all-services requirement. |

## Detailed findings

### 1. Email preview authorization is insufficient — high priority

[`ApprovedEmailPreviewService.cs`](../src/ElixomClaim.Lib/Services/ApprovedEmailPreviewService.cs) is a welcome move of preview composition into Lib. It validates active actor and minimum role, redacts recipient emails, excludes collection payor name/email/telephone from the preview HTML, and limits templates to `CollectionReceipt` and `PaymentSummary`.

It does **not** authorize the actor against the requested record:

- A Teller-role actor can preview any `CollectionTransaction` ID; the query does not require `TellerUserId == actorUserId` or an approved manager/client relationship.
- A Manager-role actor can preview any `JobPayment` ID; the query does not apply the `IJobPaymentService.GetForActorAsync` access predicate.

This is not fixed merely by moving the code into Lib—the shared service must implement the same record-level decision as MVC/queue paths. Add a `CanPreview`/projection method to the collection and job-payment service, or share their access predicates. Add cross-user preview-denial tests for both HTTP API and real MCP invocation.

### 2. Outbox wake-up is stored but never executed/completed — high priority

[`ApprovedOperationService.cs`](../src/ElixomClaim.Lib/Services/ApprovedOperationService.cs) reserves `OutboxWakeUp` and audits it. [`OutboxDispatchHostedService.cs`](../src/ElixomClaim.Web/HostedServices/OutboxDispatchHostedService.cs) does not read operation records, signal on wake-up, or update their state. It only polls due mail every 30 seconds.

The operation response/status therefore remains `Accepted`; no completion, failure, retry, or restart-recovery state is observable. That falls short of the required durable background-operation semantics. Either:

- implement a durable command consumer that signals/executes the dispatcher and marks the record `Completed`/`Failed`; or
- explicitly remove “wake-up” as an operation and document that standard polling is the only dispatch path.

### 3. REST API is materially complete but lacks OpenAPI/contract artefacts

The API now covers the resource categories stated in Sprint 12 and isolates `api:access` from `mcp:access`. Its HTTP integration suite tests unauthenticated/wrong-scope paths, owned data, role boundaries, pagination limits, approved templates, redacted previews, idempotency replay, operation ownership, real MCP discovery/invocation, and route retirement.

No `AddOpenApi`, `AddSwagger`, `MapOpenApi`, or OpenAPI/Swagger contract artefact exists. The sprint calls for documented REST contract/OpenAPI tests. Add an explicit API description and test request/response/error envelopes—including every validation branch—rather than treating controller DTOs as the entire external contract.

### 4. Durable command recovery is underspecified

Salary/API/MCP operations reserve an `Accepted` record before processing, which protects against duplicate starts. A cancellation/process termination after reservation can leave it accepted indefinitely. There is no worker/reconciliation policy that transitions stale records, retry eligibility/state, or safe recovery after restart.

Define the status state machine (`Accepted`, `Running`, `Completed`, `Failed`, retryable terminal states), recovery ownership, timeout policy, and duplicate-key behaviour. Add relational/restart tests—not only in-memory replay tests.

### 5. Payout document and notification content are less detailed than specified

[`Print.cshtml`](../src/ElixomClaim.Web/Views/JobPayments/Print.cshtml) includes claims, collections/payor data, payrolls/entries, deductions, adjustment context, and headline calculation. It uses lists, however, rather than itemized HTML tables with category subtotals.

The job-payment email composition includes claims, collections, deductions, and totals but does not include linked payrolls/payroll entries. The specification asks for recipient/bank information, totals, claims, collections, linked payrolls/entries, deductions, and itemized subtotal context. Convert print/email sections to structured tables and add render/content tests.

### 6. Audit/email record schema variations affect reporting fidelity

[`AuditRecord.cs`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs) stores a combined `Target` and `TimestampUtc`, rather than separate `EntityType`, `EntityId`, and `OccurredAtUtc`. It remains usable, but is less queryable than the specified audit model.

[`NotificationEntities.cs`](../src/ElixomClaim.Lib/Entities/NotificationEntities.cs) stores one `Recipient`, not the specified `To`, `From`, `Cc`, and `Bcc`. System-copy recipients are separate outbox rows, which is delivery-equivalent but not header/audit-schema equivalent. Decide whether literal reporting/audit requirements require a migration.

### 7. Audit-manager scope is unresolved

[`AdminController.cs`](../src/ElixomClaim.Web/Controllers/AdminController.cs) allows managers to read the latest 200 audit records and hides before/after state unless the viewer is an administrator. The Claude specification explicitly leaves unresolved whether Manager audit access should be restricted to claims/collections/job payments. The current query is unscoped metadata access, including user/OAuth/system action targets. Record and implement a decision, or scope the query by approved domains.

### 8. Production migration procedure remains an operational responsibility

The SQL Server `sp_getapplock` guard materially fixes cross-instance schema races. It is activated only when the caller passes `isProduction` and provider name exactly matches SQL Server. Confirm deployment invokes this production path and designate a migration runner. The current default auto-apply configuration makes an explicit production migration-job policy safer even with the lock.

### 9. Logging completeness and security-review work remain

Add `ILogger<T>` where noted in the matrix. Preserve existing redaction discipline: no access/refresh tokens, bank accounts, email bodies, or secrets in logs.

The custom OAuth server remains subject to the required formal threat model, interoperability testing, and independent security review before release. The repository's source/test coverage is not a substitute for that external review.

## Intentional or acceptable variations

| Source wording | Current implementation | Assessment |
| --- | --- | --- |
| int or Guid IDs | Guid technical IDs plus durable display sequences | Improvement; consistently applied. |
| Simple background queue | Transactional outbox with idempotency/retry | Reliability improvement for financial notifications. |
| No paid-record correction mechanism detailed | Linked approval-based adjustment/reversal records | Improvement preserving financial immutability. |
| Example `/mcp/sse` transport | Official stateless Streamable HTTP `/mcp` | Valid standards-conformant equivalent. |
| System copy as CC/BCC | Individual outbox/email-log recipient | Delivery-equivalent, not literal header capture. |
| `AuditLogEntry` name | `AuditRecord` | Neutral naming; structured target fields are the substantive difference. |

## Recommended completion order

1. Fix record-level authorization in approved email previews; prove cross-user denial through HTTP and MCP transport tests.
2. Make outbox wake-up a real durable command with lifecycle/recovery, or remove it from the approved operation surface.
3. Specify/implement stale-operation recovery for all durable operations and add relational restart tests.
4. Publish/test OpenAPI and full REST error-contract coverage.
5. Resolve manager audit scope, payout table/payroll-email fidelity, and structured audit/email header schema decisions.
6. Complete the remaining logging work, deployment migration-runner procedure, and independent OAuth security/interoperability review.

## Delivery-state note

`MEMORY.md` and the Sprint 12 ledger now consistently say the versioned API is retained and Sprint 12 remains in progress. This report is an assessment artefact; it neither claims nor completes a backlog item.
