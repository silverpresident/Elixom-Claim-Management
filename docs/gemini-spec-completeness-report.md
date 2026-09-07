# Gemini Specification Completeness Report

**Re-evaluated:** 2026-09-07

**Source specification:** [`context/gemini-specs.md`](../context/gemini-specs.md)

**Method:** Code, migration, runtime-wiring, package, sprint-ledger, and automated-test review. This report reflects the current checkout rather than the 2026-09-03 assessment.

## Executive conclusion

The implementation is now **substantially complete for the core business application**, but it currently has release-blocking test failures. The earlier functional gaps in profile/bank management, claim job date, collection telephone, job-payment UI, salary adjustment UI, payroll custom-entry UI, audit immutability, rate limiting, OAuth consent persistence, and migration startup have been addressed. The latest changes also add durable human-facing record numbers, stronger bank-detail fields, richer payout/receipt presentation, transaction snapshots for teller-entered collection values, and corrected collection-fee allocation for job-payment totals.

It is **not fully complete against `gemini-specs.md`**, but the former primary MCP transport gap is now closed. The host registers the official SDK's stateless Streamable HTTP transport and exposes the sole MCP endpoint at `/mcp`; it is Bearer-authenticated, requires `mcp:access`, and is rate limited. All six domain tool groups are discoverable through MCP attributes, and the legacy `/mcp/*` MVC controller surface has been removed. Sprint 12 has completed its base tool-contract/attribution hardening; its next durable, actor-owned operations boundary is currently marked blocked pending ledger reconciliation. The separately scoped `/api/v1` REST API remains unimplemented.

## Requirement coverage

| Requirement area | Status | Evidence |
| --- | --- | --- |
| .NET 10 MVC, Lib/Web/test split, EF Core, Azure SQL `dbclaim`, JMD money precision | Implemented | [`ApplicationDbContext.cs`](../src/ElixomClaim.Lib/Data/ApplicationDbContext.cs), [`DependencyInjection.cs`](../src/ElixomClaim.Lib/DependencyInjection.cs) |
| Google allow-list authentication, blocked-user denial, hierarchical application roles | Implemented | [`UserValidationEvents.cs`](../src/ElixomClaim.Web/Authentication/UserValidationEvents.cs), [`AuthorizationHandlers.cs`](../src/ElixomClaim.Lib/Authorization/AuthorizationHandlers.cs) |
| Claims lifecycle, soft deletion, job date, comments, claimant dashboard/history | Mostly implemented | [`ClaimEntities.cs`](../src/ElixomClaim.Lib/Entities/ClaimEntities.cs), [`ClaimsController.cs`](../src/ElixomClaim.Web/Controllers/ClaimsController.cs); durable `SequenceNo` values provide safe human-facing labels while Guids remain technical keys. |
| Profile and bank-detail management with masked display, optional display name, branch name/account type, and audit logging | Implemented | [`ProfileController.cs`](../src/ElixomClaim.Web/Controllers/ProfileController.cs), [`Profile/Index.cshtml`](../src/ElixomClaim.Web/Views/Profile/Index.cshtml), migration `20260907090000_AddUserProfileDisplayAndBankFields` |
| Collections, client options/fees, complete client bank details, payor email/telephone, receipts, HTML print/reissue | Mostly implemented | [`CollectionEntities.cs`](../src/ElixomClaim.Lib/Entities/CollectionEntities.cs), [`CollectionClientAdministrationService.cs`](../src/ElixomClaim.Lib/Services/CollectionClientAdministrationService.cs), [`CollectionService.cs`](../src/ElixomClaim.Lib/Services/CollectionService.cs), and [`JobPaymentService.cs`](../src/ElixomClaim.Lib/Services/JobPaymentService.cs): transaction fees are immutable client-configured snapshots; a collection job applies its client's per-job fee once and sums attached transaction snapshots. The workflow intentionally permits transaction-only custom purpose/amount values, differing from Gemini's configured-choice-only workflow. |
| Durable SMTP/ACS outbox, retries, idempotency, email logs, HTML-only notifications | Implemented | [`OutboxService.cs`](../src/ElixomClaim.Lib/Services/OutboxService.cs), [`EmailSenders.cs`](../src/ElixomClaim.Lib/Services/EmailSenders.cs) |
| Job creation, attachment/removal, deductions, metadata, submit/schedule/settle, adjustment workflow | Implemented | [`JobPaymentService.cs`](../src/ElixomClaim.Lib/Services/JobPaymentService.cs), [`JobPaymentsController.cs`](../src/ElixomClaim.Web/Controllers/JobPaymentsController.cs); detail/print presentation now includes linked collection, payroll, entry, deduction, and adjustment context. |
| Salary definitions, adjustments, recurrence engine, payroll custom entries and payroll-to-job flow | Implemented | [`SalaryPayrollService.cs`](../src/ElixomClaim.Lib/Services/SalaryPayrollService.cs), [`PayrollController.cs`](../src/ElixomClaim.Web/Controllers/PayrollController.cs) |
| Audit redaction and database-level append-only protection | Implemented | [`AddAuditRecordAppendOnlyTrigger.cs`](../src/ElixomClaim.Lib/Migrations/20260903090000_AddAuditRecordAppendOnlyTrigger.cs) |
| OAuth authorization code + PKCE, consent persistence, token rotation/revocation, configured lifetimes and rate limiting | Mostly implemented | [`OAuthService.cs`](../src/ElixomClaim.Lib/Services/OAuthService.cs), [`OAuthController.cs`](../src/ElixomClaim.Web/Controllers/OAuthController.cs), [`RateLimitingConfiguration.cs`](../src/ElixomClaim.Web/Configuration/RateLimitingConfiguration.cs); Sprint 12 adds an `api:access` contract but not live API enforcement. |
| Guarded application-start migration execution | Implemented with deployment qualification | [`Program.cs`](../src/ElixomClaim.Web/Program.cs), [`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) |
| Standard MCP server transport and tool discovery/invocation | **Implemented; durable operations boundary pending** | [`Program.cs`](../src/ElixomClaim.Web/Program.cs) calls `AddMcpServer().WithHttpTransport().WithTools<…>()` and maps `/mcp` with the Bearer `McpAccess` policy and MCP rate limit. The six classes in [`Mcp/Tools`](../src/ElixomClaim.Web/Mcp/Tools) use `McpServerToolType`/`McpServerTool`; [`McpToolActorAccessor.cs`](../src/ElixomClaim.Web/Mcp/Tools/McpToolActorAccessor.cs) delegates concrete actor resolution/audit context to [`ActorResolver.cs`](../src/ElixomClaim.Web/Services/ActorResolver.cs). Sprint 12 item 4 completed stable tool names, cancellation propagation, safe adapter errors, correlated audit attribution, and basic redaction tests; item 5 must replace direct operations execution with a durable actor-owned command boundary. |
| CDN Bootstrap/jQuery, refined plum-and-gold SVG favicon, responsive HTML print, privacy page | Implemented | [`favicon.svg`](../src/ElixomClaim.Web/wwwroot/favicon.svg), [`_Layout.cshtml`](../src/ElixomClaim.Web/Views/Shared/_Layout.cshtml), [`Privacy.cshtml`](../src/ElixomClaim.Web/Views/Home/Privacy.cshtml) |

## Remaining differences and risks

### 1. MCP transport and base tool adapters are implemented; durable operations are incomplete

The Gemini example endpoint is `/mcp/sse`; the implementation uses the current official SDK's standard stateless Streamable HTTP endpoint at **`/mcp`** instead. This is an acceptable protocol evolution, not a missing transport.

- `Program.cs` registers `ModelContextProtocol.AspNetCore` with `AddMcpServer().WithHttpTransport()` and all six tool groups, then maps only `/mcp` through `MapMcp`.
- The endpoint requires the custom OAuth Bearer scheme, authenticated `mcp:access`, and the MCP rate-limit policy. `McpToolActorAccessor` resolves the active database user with the shared `IActorResolver`, preserving scope, role, correlation ID, IP address, and `IsMcp` audit classification.
- The six former proprietary `Mcp*Controller` adapters are absent. A repository route search finds no live `/mcp/*` controller replacement, and no `/api/v1` endpoint has been added yet.
- Tool discovery currently exposes 14 stable names across Claims, Collections, Job Payments, Payroll, Email, and Operations; `McpToolContractTests` asserts the names and basic collection-data redaction.

Sprint 12 item 4 is recorded complete: stable names are tested, tool handlers resolve the concrete actor through the shared resolver, read adapters use safe failure responses and cancellation propagation, and collection DTOs omit payor email and internal processing fees. The remaining concerns are narrower but material:

- Several adapters still query `ApplicationDbContext` and compose/queue outbox messages directly instead of delegating every domain decision to a shared Lib service. That conflicts with the stated thin-adapter boundary.
- Invocations currently produce both adapter-level `MCP_TOOL_*` audit records and underlying `MCP_*` audit records, so a single request can be audited more than once. The intended single, complete attribution model needs to be settled and tested.
- `operations_outbox_wakeup` directly calls `IOutboxService.DispatchDueAsync`, which is dispatch work rather than a durable request for background work. `operations_status` retrieves by idempotency key without an apparent actor-ownership check. Both points need correction before the operations tools meet the MCP guardrails.

### 2. Claim comments are not threaded

`ClaimComment` has claim and author references but no parent-comment/thread reference. The application supports chronological public and private comments, satisfying the newer README requirement, but not Gemini's explicit "threaded comments" requirement.

### 3. Collection configuration is now suggestions rather than an exclusive catalog

Gemini specifies that tellers choose configured purpose and amount values. The current collection UI presents client-scoped suggestions but also permits free-text purpose and custom amount input. The shared service preserves a custom entry as an immutable transaction snapshot and does not modify client configuration, which is a sound auditability choice, but it relaxes the specification's catalog-only validation rule.

If client-configured values are intended to be mandatory financial controls, custom entries should be removed or separately authorized/audited as exceptions.

### 4. OAuth policy enforcement remains incomplete

The hardening improvements are real: redirect URI validation, persisted consent, PKCE S256, raw-code non-retention, configured lifetimes, replay revocation, and rate limiting are present.

Two policy boundaries remain unclear or absent in code:

- Requested authorization scopes are not checked against `OAuthClient.AllowedScopes` before consent/code issuance.
- The token endpoint describes `client_secret_post` during registration, but code/refresh exchanges treat the client secret as optional. That can be valid for explicitly configured public clients with PKCE, but the application currently has no explicit public-versus-confidential client policy.
- Although `api:access` was added to the OAuth defaults and the transport contract says it is separate from `mcp:access`, the default authorization request asks for both scopes and no `/api/v1` endpoint exists to enforce the separation. The live MCP endpoint does enforce `mcp:access`; REST scope isolation remains contractual groundwork.

These are protocol-hardening issues rather than missing business workflows.

### 5. Migration locking is process-local

`ApplyDatabaseMigrationsAsync()` is wired on non-Development startup and honours `AutoApplyMigrations`, but its `SemaphoreSlim` prevents concurrent migration attempts only within one process. It does not coordinate multiple deployed instances. Production safety therefore still depends on the documented external single-runner deployment topology.

### 6. Logging coverage is incomplete

The specification requires `ILogger<T>` in controllers, services, and hosted services. Static inspection finds no such dependency in `AdminController`, `ClaimsController`, `HomeController`, `JobPaymentsController`, or `ManagerClaimsController`, and none of the six MCP tool adapters has one. This does not remove the existing audit trail, but it leaves the structured operational logging requirement incomplete.

### 7. Release, test, and documentation risks

- The current full suite does **not** pass. `dotnet test ElixomClaim.slnx --no-restore` fails to compile `WorkflowFieldsCompletionTests` because it still initializes the removed `RecordCollectionInput.ProcessingFee` property. The test must instead configure the client's `PerTransactionFee` and assert the service-owned snapshot.
- The Lib-only suite also has one failing SQL Server relational audit test: applying the clean migration baseline fails because schema `dbclaim` does not exist or cannot be used. The run result was **118 passed, 1 failed**. This is the known migration/schema-order release blocker, now reproduced in this review.
- `dotnet list ... package --vulnerable --include-transitive` reports a **high-severity** transitive `SSH.NET` vulnerability (`GHSA-q939-rpr3-3284`) in `ElixomClaim.Lib.Tests`.
- The top-level README still says the implementation "has not yet been scaffolded," which is materially outdated.
- Sprint 12 items 1–4 are complete. Item 5 is marked `Blocked` by prerequisite 5a, while the same ledger records 5a `Complete`; the status/reason needs reconciliation before work resumes. Items 6–8, including the separately scoped `/api/v1` API and its integration/contract evidence, are not started.
- The collection-client bank-detail migration preserves existing records with empty branch-name/account-type fields. An Administrator must correct those legacy rows before they are relied on for a payout.
- The independent OAuth security review remains an open risk in `MEMORY.md`; no code-only review can close it.

## Intentional and acceptable variations

| Gemini specification | Current implementation | Assessment |
| --- | --- | --- |
| `AuditLogs` | `AuditRecords` | Naming variation; append-only trigger now provides the required integrity control. |
| In-memory `Channel<T>` email queue | Durable database outbox and hosted dispatcher | Stronger reliability and idempotency model. |
| `/Teller/PrintReceipt/{id}` | `/collections/{id}/print` | Equivalent feature under a different route. |
| No adjustment process specified for paid records | Linked, audited adjustment/reversal workflow | Additional financial safeguard. |
| Older numeric-style identifiers implied by examples | Uniform Guid identifiers | Valid implementation choice with an ADR and migration strategy. |
| Opaque Guid labels in the UI | Durable database-sequence-backed `SequenceNo` values, while Guids remain route/FK/audit keys | Usability improvement without changing technical identity. |

## Verification performed on 2026-09-07

```bash
dotnet test ElixomClaim.slnx --no-restore
dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore
dotnet list ElixomClaim.slnx package --vulnerable --include-transitive
```

- The full suite failed during Web-test compilation: `WorkflowFieldsCompletionTests.cs` references the deleted `RecordCollectionInput.ProcessingFee` property. No current passing Web-suite total can therefore be claimed; the earlier **71 passed** result pre-dates the fee-authority change.
- The Lib-only suite compiled and ran: **118 passed, 1 failed**. The failure is `AuditRecords_RejectUpdatesAndDeletesAtTheSqlServerBoundary`, which cannot apply the clean relational migration baseline because `dbclaim` is unavailable at the point it is used.
- The package scan confirmed the high-severity transitive `SSH.NET` advisory in the Lib test project and the two existing `NU1510` unnecessary-options-package warnings. The test runs also emit the existing obsolete Testcontainers builder warning.
- Sprint 12 historical evidence remains useful for its completed MCP work (186 non-integration tests for item 3 and 13 focused MCP tests for item 4), but it is not a substitute for the now-failing current full suite. Broader MCP transport/authorization integration and contract testing remains unimplemented.

## Overall assessment

The product is now **functionally close to complete** against the Gemini business specification. The current authenticated, standard MCP transport and discovered tools close the previous protocol-level gap; the legacy proprietary MCP controller surface is retired. It should not be represented as fully complete or production-release ready until the current Web-test compilation regression and relational migration-baseline failure are fixed, Sprint 12 finishes its durable operations boundary and `/api/v1` scope separation, OAuth client/scope policy is made explicit, threaded comments and the configured-choice collection variation are consciously resolved, and the outstanding logging, dependency, migration-topology, and independent-security-review risks are closed.
