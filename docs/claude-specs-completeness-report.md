# Claude Specification Completeness Report

**Reviewed:** 2026-09-08

**Specification:** [`context/claude-specs.md`](../context/claude-specs.md)
**Method:** Static review of current projects, domain/services, migrations, routes, Razor, configuration, test inventory, sprint evidence, and current test execution. This report describes the worktree as reviewed; sprint rows are supporting evidence, not proof by themselves.

## Conclusion

The repository contains a substantial implementation of the specification. The intended Lib/Web split, Google-only provisioned-user sign-in, hierarchical roles, core claims/collections/job-payment/payroll workflows, durable outbox, OAuth authorization-code flow, standard MCP endpoint, privacy page, and CDN-only frontend are present.

It is **not complete or release-ready**. The most important blockers are:

1. The EF migration history cannot create a clean SQL database: the reset initial migration creates foreign keys to tables that it creates later (or never creates in that migration), and its original `long` schema conflicts with the current Guid model.
2. MCP operations execute their side effect before their durable idempotency record is created, and operation-status reads do not enforce record ownership.
3. The Sprint 12 `/api/v1` API required by the current delivery contract is absent, and no real MCP protocol lifecycle/integration coverage was found.
4. The specification's universal `ILogger<T>` requirement and its thin-adapter/service-boundary rule are only partially met.

## Current verification

The following command passed in the reviewed worktree:

```bash
dotnet test ElixomClaim.slnx --no-restore --filter "Category!=Integration" --logger "console;verbosity=minimal"
```

Results: **118 Lib tests passed** and **71 Web tests passed** (189 total). Warnings remain: `SSH.NET` 2025.1.0 has a high-severity advisory (`GHSA-q939-rpr3-3284`), and the Lib project has two `NU1510` package-reference warnings.

An unfiltered suite was started, but the relational/Testcontainers portion did not finish within the command window. It must not be represented as a current clean-SQL verification. The migration source itself independently demonstrates the clean-schema defect described below.

## Requirement status

| Specification area | Status | Evidence and variation |
| --- | --- | --- |
| .NET 10 MVC solution; Lib/Web/test projects; EF Core; `dbclaim` schema | Mostly implemented | All four projects exist and the DbContext targets `dbclaim`. The clean-database migration chain is broken, so the database delivery requirement is not complete. |
| Consistent Guid identifiers | Implemented variation | The spec allows either int or Guid consistently. Current entities/routes use Guid technical IDs plus sequence-backed display numbers, which is an improvement in usability. |
| Money, JMD, UTC | Mostly implemented | Model mappings/services use `decimal(18,2)`, JMD, and UTC fields. The legacy initial migration is inconsistent with the current model, preventing a release-ready persistence conclusion. |
| Google SSO, provisioned users, bootstrap administrator | Implemented | Cookie + Google authentication, active-user validation, and bootstrap seeding are wired in `Program.cs`, `UserValidationEvents`, and `DatabaseMigrationExtensions`. |
| Single hierarchical role and policies | Implemented | `UserRole`, role extensions, policies, and shared authorization handlers implement the stated inheritance model. |
| Claims lifecycle, ownership, comments, soft deletion | Mostly implemented | Entities, `ClaimService`, MVC views/controllers, and tests cover draft/submitted editing, management decisioning, comments, and default soft-delete filtering. Comment append-only behaviour is service behaviour rather than a database-level immutability constraint. |
| User profile, display name, bank information, payment history | Implemented | Profile UI/service data provide optional display name, required bank branch/account type, and masked views; the dashboard exposes payment history. |
| Collection clients/configuration and clearing-house workflow | Mostly implemented | Admin-managed clients/options/bank details, client assignment, teller capture, immutable fee snapshots, custom purpose/amount snapshots, receipt queueing/reissue, and HTML print are implemented. |
| Job-payment composition, lifecycle and paid cascade | Mostly implemented | The shared service enforces one payee type, Processing-only edits, totals, scheduling/settlement, outbox queueing, and claim/collection/payroll cascades. A detailed payout presentation remains partial. |
| Salary definitions, recurrence, generated payroll and submit-to-job | Implemented | Planner, salary service, daily hosted scheduler, accountant workspace, locked generated entries, custom-entry validation, and bound job-payment creation are present. |
| SMTP/ACS email, durable outbox, retries and email logs | Mostly implemented | Sender abstractions, outbox worker, retries, skipped-recipient handling, and logs exist. `EmailLog` is not a literal match for the specified `To`/`From`/`Cc`/`Bcc` shape. |
| Custom OAuth server, authorization code, PKCE S256, refresh/revocation, consent | Mostly implemented | OAuth controller/service/entities provide registration, consent, code exchange, rotation/revocation, and scope handling. Formal interoperability/security review remains required. |
| Standard MCP transport and domain tool groups | Mostly implemented | Official SDK registration maps authenticated/rate-limited Streamable HTTP at `/mcp`; six grouped tool classes expose 14 attributed tools. See material MCP gaps below. |
| `/api/v1` REST API with separate `api:access` scope | Not implemented | `api:access` is defined, but no `/api/v1` endpoint/controller/API route exists. This is Sprint 12 items 6–8. |
| Audit persistence and append-only enforcement | Mostly implemented | Audit service/redaction and the SQL UPDATE/DELETE prevention trigger exist. The data model is less structured than specified and cannot yet be verified from a clean SQL migration. |
| Privacy, HTML-only output, CDN assets, favicon, scaffold cleanup | Implemented | Privacy page/footer, SVG favicon, CDN Bootstrap/jQuery with SRI, and print views exist; no `Class1.cs` or local frontend library distribution was found. |
| Logging in every controller/service/hosted service | Partial | Both hosted services log, as do several services/controllers. Multiple concrete adapters omit `ILogger<T>`; details below. |

## Material gaps and differences

### 1. Clean SQL migration path is broken — release blocker

[`20260903053340_InitialCreate.cs`](../src/ElixomClaim.Lib/Migrations/20260903053340_InitialCreate.cs) creates `JobPayments` before `CollectionClients` and `Users`, while adding foreign keys to those tables. It also creates legacy `long` primary-key columns despite the current Guid entity convention. The sprint ledger accurately flags this as Sprint 12 prerequisite 5b.

Consequences:

- An empty Azure SQL database cannot be considered deployable from the checked-in migration ledger.
- SQL trigger/audit persistence and durable-operation restart claims cannot be accepted as release evidence until migrations apply to an empty SQL instance.
- Do not rewrite deployed financial migrations destructively. Use a documented, data-preserving compatibility/baseline reconciliation with relational tests.

### 2. MCP durable-operation behaviour is non-compliant — high priority

[`OperationsTools.cs`](../src/ElixomClaim.Web/Mcp/Tools/OperationsTools.cs) checks for an existing operation, then calls salary generation or `IOutboxService.DispatchDueAsync`, and only afterwards writes the operation record. A restart or concurrent request between the side effect and record write can repeat work. This fails the requirement that the operation is durable/idempotent before approved work is requested.

The same class has a second authorization defect: `GetOperationStatusAsync` fetches by idempotency key but never verifies `record.ActorUserId == actor.Id`. Any authenticated MCP user who knows or guesses a key can read another actor's operation details.

The outbox tool invokes `DispatchDueAsync` directly. Although it does not reach the hosted-service type, it still performs worker work synchronously rather than placing an approved durable command for the worker/durable command boundary. This is contrary to the requirement to request, rather than execute, background work.

### 3. MCP adapters do not consistently preserve shared-service authorization boundaries

The tools are correctly grouped and actor-resolved, but several directly query `ApplicationDbContext`, notably [`EmailTools.cs`](../src/ElixomClaim.Web/Mcp/Tools/EmailTools.cs). For example, collection preview/queue checks only Teller-or-higher and loads a transaction by ID; it does not apply the recording-teller/manager ownership check used by `CollectionService.ReissueReceiptAsync`. Email queueing also composes/persists outbox records directly instead of using a shared service.

This is a sensitive-data and consistency risk: the specification requires the MCP identity to inherit the same concrete authorization/business decisions as the UI, not merely a comparable minimum role. Move these projections/commands into authorization-aware Lib services (or add equivalent shared ownership checks with tests).

### 4. REST API replacement is absent; MCP integration evidence is incomplete

The runtime maps `/mcp` with Bearer authentication, `mcp:access`, and MCP rate limiting, and does not retain legacy `/mcp/*` MVC controllers. That is a positive correction.

However, a repository search found no `/api/v1` mapping, API controller, or `ApiController` implementation. The separate `api:access` scope therefore has no consumer. The ongoing Sprint 12 contract explicitly requires claims, collections, job payments, constrained email, payroll, and operation REST capabilities.

Tests cover tool attributes/contracts and adapter boundaries, but no in-process conforming MCP client/protocol test was found for initialize, discovery, invocation, cancellation, invalid/revoked/missing scope, and transport error behaviour. Attribute tests do not prove Streamable HTTP interoperability.

### 5. Migration runner is only process-local

[`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) protects startup migration with a static `SemaphoreSlim`. That prevents same-process concurrency only; two production instances or deployment jobs can still migrate concurrently. The specification requires one migration runner/instance. Enforce deployment topology or a database/distributed lock, and make production migration authority explicit.

### 6. Audit and email-log schemas differ from the specification

- [`AuditRecord.cs`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs) stores a combined `Target` rather than separate `EntityType` and `EntityId`; it has `TimestampUtc` rather than `OccurredAtUtc`. This is workable but weaker for structured querying/reporting than the specified record.
- [`NotificationEntities.cs`](../src/ElixomClaim.Lib/Entities/NotificationEntities.cs) has one `Recipient` and no `From`, `Cc`, or `Bcc` fields. System copies are individual outbox messages, which is delivery-equivalent but not header/audit-schema equivalent.
- The append-only trigger is a material security improvement, but it cannot be credited as clean-environment verified until gap 1 is resolved.

### 7. Payout output is only partially complete

The job print view includes linked claims, collections, payrolls/entries, deductions, adjustment context, and total calculation. It uses simple lists and does not provide the specified itemized HTML tables with category subtotals. [`EmailTools.cs`](../src/ElixomClaim.Web/Mcp/Tools/EmailTools.cs) likewise composes list-based payout HTML and omits payroll items. The required complete payout summary—recipient/bank information, totals, claims, collections, payrolls/entries, deductions, and subtotals—is therefore partial.

### 8. Universal logging requirement is not met

Concrete files without an `ILogger<T>` dependency include:

- Controllers: `AdminController`, `ClaimsController`, `HomeController`, `JobPaymentsController`, and `ManagerClaimsController`.
- Services: `OperationRecordService`, `SalaryRecurrencePlanner`, and `SystemClock` (the latter two may reasonably be treated as pure utility components, but the specification is literal).
- All MCP tool classes and `McpToolActorAccessor`.

The hosted services meet the requirement. Add structured, redacted logging to the remaining adapters/services, avoiding email bodies, account numbers, secrets, and tokens.

### 9. OAuth and release hardening remain outstanding

The custom OAuth implementation has meaningful controls, but its own specification requires formal threat-model/interoperability/independent security review before release. The repository’s threat model and sprint risks reflect that. This report does not treat source-level unit tests as a substitute for that review.

The current test run also reports a high-severity `SSH.NET` advisory. Resolve or replace the dependency before release.

## Intentional or acceptable variations

| Specification wording | Current implementation | Assessment |
| --- | --- | --- |
| IDs may be int or Guid | Guid technical IDs plus durable sequence display numbers | Better than either bare option; consistent in the current model. |
| Simple email queue | Durable transactional outbox with retries/idempotency | Security/reliability improvement. |
| No paid-record correction workflow stated | Auditable linked adjustment/reversal workflow | Improvement that preserves paid-record immutability. |
| Example MCP endpoint `/mcp/sse` | Standard stateless Streamable HTTP `/mcp` | Valid modern transport variation. |
| System copy as CC/BCC | Separate recipient/outbox item | Delivery-equivalent, but email-log header fidelity is incomplete. |
| `AuditLogEntry` naming | `AuditRecord` | Neutral naming change; split entity fields remain the substantive gap. |

## Recommended completion order

1. Complete Sprint 12 prerequisite 5b: reconcile migrations with a data-preserving plan and prove empty-SQL migration plus relational trigger/audit/operation tests.
2. Refactor MCP operations to create actor-owned durable commands before side effects; enforce ownership on status reads; have workers/domain services execute queued commands.
3. Move MCP email/read authorization into shared Lib services and add cross-user/role redaction/ownership integration tests.
4. Implement the scope-isolated, documented `/api/v1` surface and protocol-level MCP integration tests; finish Sprint 12 items 6–8.
5. Add missing `ILogger<T>` coverage and improve payout HTML tables/subtotals, structured audit fields, and email-header logging if literal specification fidelity is required.
6. Resolve the vulnerable dependency, introduce a multi-instance-safe migration authority, and obtain the prescribed OAuth security/interoperability review before release.

## Delivery-ledger note

`MEMORY.md` correctly identifies Sprint 12 as in progress and records the migration blocker. This report updates an audit artifact only; it does not claim or alter a Sprint 12 backlog item, and it must not be used to mark the sprint complete.
