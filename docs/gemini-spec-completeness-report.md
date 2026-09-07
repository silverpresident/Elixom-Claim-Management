# Gemini Specification Completeness Report

**Re-evaluated:** 2026-09-07

**Source specification:** [`context/gemini-specs.md`](../context/gemini-specs.md)

**Method:** Code, migration, runtime-wiring, package, sprint-ledger, and automated-test review. This report reflects the current checkout rather than the 2026-09-03 assessment.

## Executive conclusion

The implementation is now **substantially complete for the core business application**. The earlier functional gaps in profile/bank management, claim job date, collection telephone, job-payment UI, salary adjustment UI, payroll custom-entry UI, audit immutability, rate limiting, OAuth consent persistence, and migration startup have been addressed. The latest changes also add durable human-facing record numbers, stronger bank-detail fields, richer payout/receipt presentation, and transaction snapshots for teller-entered collection values.

It is **not fully complete against `gemini-specs.md`**, chiefly because no actual MCP server transport is registered or mapped. Sprint 12 has added a documented `/mcp`/`/api/v1` contract, `api:access`, and a tested shared `IActorResolver`, but the host still has no `AddMcpServer`/`MapMcp` calls, MCP tool annotations, `/mcp` endpoint, or `/api/v1` endpoints. The legacy bearer-authenticated REST controllers under `/mcp/*` remain the live integration surface.

## Requirement coverage

| Requirement area | Status | Evidence |
| --- | --- | --- |
| .NET 10 MVC, Lib/Web/test split, EF Core, Azure SQL `dbclaim`, JMD money precision | Implemented | [`ApplicationDbContext.cs`](../src/ElixomClaim.Lib/Data/ApplicationDbContext.cs), [`DependencyInjection.cs`](../src/ElixomClaim.Lib/DependencyInjection.cs) |
| Google allow-list authentication, blocked-user denial, hierarchical application roles | Implemented | [`UserValidationEvents.cs`](../src/ElixomClaim.Web/Authentication/UserValidationEvents.cs), [`AuthorizationHandlers.cs`](../src/ElixomClaim.Lib/Authorization/AuthorizationHandlers.cs) |
| Claims lifecycle, soft deletion, job date, comments, claimant dashboard/history | Mostly implemented | [`ClaimEntities.cs`](../src/ElixomClaim.Lib/Entities/ClaimEntities.cs), [`ClaimsController.cs`](../src/ElixomClaim.Web/Controllers/ClaimsController.cs); durable `SequenceNo` values provide safe human-facing labels while Guids remain technical keys. |
| Profile and bank-detail management with masked display, optional display name, branch name/account type, and audit logging | Implemented | [`ProfileController.cs`](../src/ElixomClaim.Web/Controllers/ProfileController.cs), [`Profile/Index.cshtml`](../src/ElixomClaim.Web/Views/Profile/Index.cshtml), migration `20260907090000_AddUserProfileDisplayAndBankFields` |
| Collections, client options/fees, complete client bank details, payor email/telephone, receipts, HTML print/reissue | Mostly implemented | [`CollectionEntities.cs`](../src/ElixomClaim.Lib/Entities/CollectionEntities.cs), [`CollectionClientAdministrationService.cs`](../src/ElixomClaim.Lib/Services/CollectionClientAdministrationService.cs), [`CollectionService.cs`](../src/ElixomClaim.Lib/Services/CollectionService.cs); the workflow intentionally now permits transaction-only custom purpose/amount values, which differs from Gemini's configured-choice-only workflow. |
| Durable SMTP/ACS outbox, retries, idempotency, email logs, HTML-only notifications | Implemented | [`OutboxService.cs`](../src/ElixomClaim.Lib/Services/OutboxService.cs), [`EmailSenders.cs`](../src/ElixomClaim.Lib/Services/EmailSenders.cs) |
| Job creation, attachment/removal, deductions, metadata, submit/schedule/settle, adjustment workflow | Implemented | [`JobPaymentService.cs`](../src/ElixomClaim.Lib/Services/JobPaymentService.cs), [`JobPaymentsController.cs`](../src/ElixomClaim.Web/Controllers/JobPaymentsController.cs); detail/print presentation now includes linked collection, payroll, entry, deduction, and adjustment context. |
| Salary definitions, adjustments, recurrence engine, payroll custom entries and payroll-to-job flow | Implemented | [`SalaryPayrollService.cs`](../src/ElixomClaim.Lib/Services/SalaryPayrollService.cs), [`PayrollController.cs`](../src/ElixomClaim.Web/Controllers/PayrollController.cs) |
| Audit redaction and database-level append-only protection | Implemented | [`AddAuditRecordAppendOnlyTrigger.cs`](../src/ElixomClaim.Lib/Migrations/20260903090000_AddAuditRecordAppendOnlyTrigger.cs) |
| OAuth authorization code + PKCE, consent persistence, token rotation/revocation, configured lifetimes and rate limiting | Mostly implemented | [`OAuthService.cs`](../src/ElixomClaim.Lib/Services/OAuthService.cs), [`OAuthController.cs`](../src/ElixomClaim.Web/Controllers/OAuthController.cs), [`RateLimitingConfiguration.cs`](../src/ElixomClaim.Web/Configuration/RateLimitingConfiguration.cs); Sprint 12 adds an `api:access` contract but not live API enforcement. |
| Guarded application-start migration execution | Implemented with deployment qualification | [`Program.cs`](../src/ElixomClaim.Web/Program.cs), [`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) |
| Standard MCP server transport and tool discovery/invocation | **Not implemented; groundwork added** | Package reference and shared [`ActorResolver.cs`](../src/ElixomClaim.Web/Services/ActorResolver.cs) exist, but no standard MCP registration/mapping or tool discovery is present; legacy REST controllers remain in [`Controllers`](../src/ElixomClaim.Web/Controllers). |
| CDN Bootstrap/jQuery, refined plum-and-gold SVG favicon, responsive HTML print, privacy page | Implemented | [`favicon.svg`](../src/ElixomClaim.Web/wwwroot/favicon.svg), [`_Layout.cshtml`](../src/ElixomClaim.Web/Views/Shared/_Layout.cshtml), [`Privacy.cshtml`](../src/ElixomClaim.Web/Views/Home/Privacy.cshtml) |

## Remaining differences and risks

### 1. MCP transport is absent

This is the primary remaining implementation gap.

- The Gemini specification requires Model Context Protocol interaction through an MCP transport endpoint, exemplified as `/mcp/sse`.
- The project references `ModelContextProtocol.AspNetCore` 2.2.0, but no source code calls MCP registration or mapping APIs.
- None of the six tool classes have MCP server tool annotations.
- A transport-neutral [`IActorResolver`](../src/ElixomClaim.Web/Services/ActorResolver.cs) now resolves active bearer users, scopes, correlation ID, IP address, and audit classification. It is registered and unit-tested, but no current MCP or REST adapter consumes it.
- The runtime instead exposes legacy REST adapters at `/mcp/claims`, `/mcp/collections`, `/mcp/email`, `/mcp/job-payments`, `/mcp/operations`, and `/mcp/payroll`.

The REST adapters do enforce bearer authentication and mostly enforce the `mcp:access` scope, but they are not a discoverable/invocable standard MCP server. Sprint 12 now correctly records this as an active corrective delivery item rather than a completed feature.

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
- Although `api:access` was added to the OAuth defaults and the transport contract says it is separate from `mcp:access`, the default authorization request now asks for both scopes and no `/api/v1` endpoint exists to enforce the separation. Scope isolation is therefore contractual groundwork, not live behavior.

These are protocol-hardening issues rather than missing business workflows.

### 5. Migration locking is process-local

`ApplyDatabaseMigrationsAsync()` is wired on non-Development startup and honours `AutoApplyMigrations`, but its `SemaphoreSlim` prevents concurrent migration attempts only within one process. It does not coordinate multiple deployed instances. Production safety therefore still depends on the documented external single-runner deployment topology.

### 6. Release and documentation risks

- `dotnet list ... package --vulnerable --include-transitive` reports a **high-severity** transitive `SSH.NET` vulnerability (`GHSA-q939-rpr3-3284`) in `ElixomClaim.Lib.Tests`.
- The top-level README still says the implementation "has not yet been scaffolded," which is materially outdated.
- Sprint 12 is now explicitly in progress to correct the unmapped MCP transport and replace the legacy `/mcp/*` controllers with a separately scoped `/api/v1` API. Items 1 and 2 are complete; the endpoint/tool/API items are not started.
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
dotnet test src/ElixomClaim.Web.Tests/ElixomClaim.Web.Tests.csproj --no-restore
```

- The current Web test suite passed: **70 passed, 0 failed**.
- Sprint 12 records focused passing evidence for the new profile, receipt, free-entry collection, bank-detail, sequence-number, and development-seed changes. A full solution run was not repeated for this report.
- The last package scan recorded a vulnerable transitive `SSH.NET` test dependency and two unnecessary `Microsoft.Extensions.Options` package references; re-run the scan in a normal build environment before release.

## Overall assessment

The product is now **functionally close to complete** against the Gemini business specification. All material web/domain workflow gaps identified in the prior report have been addressed. It should not be represented as fully MCP-compliant or production-release complete until a real authenticated standard MCP transport is registered, the legacy REST surface is retired or explicitly retained as a supported compatibility API, OAuth client/scope policy is made explicit, and the outstanding dependency/security-review risks are resolved.
