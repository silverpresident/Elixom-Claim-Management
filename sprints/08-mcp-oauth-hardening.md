# Sprint 08 — MCP Transport and OAuth Hardening

## Prerequisites

- Sprint 01 item 4a must be complete first. `AuditRecords` must be immutable at the database boundary before this sprint relies on them for durable security and operation evidence.
- Record any material protocol, persistence, or client-compatibility choice in an ADR before implementation.

## Ordered backlog

1. Replace the bespoke bearer-authenticated `/mcp/*` REST adapter surface with a registered, standard .NET MCP server transport and register the existing domain-scoped tools (`ClaimTools`, `CollectionTools`, `JobPaymentTools`, `PayrollTools`, `EmailTools`, and `OperationsTools`) through that transport. Keep tools as thin adapters over shared authorization-aware Lib services; remove or retire redundant MCP-labelled REST routes only with a documented compatibility decision.
2. Replace process-local operation tracking with a durable `dbclaim` operation record, idempotency constraint, audited request service, and status query. MCP operation requests must enqueue/trigger only approved domain work and must never invoke worker internals directly. Verify recovery and status visibility after a process restart.
3. Complete OAuth hardening: use validated `OAuthOptions` access/refresh lifetimes, impose an explicit dynamic-registration admission policy and redirect-URI shape validation, persist consent records, stop retaining raw authorization codes, and revalidate registered client/redirect data on consent POST before redirecting. Add the required migrations and safe audit events.
4. Add an application-level rate-limit/throttle policy for MVC and OAuth/MCP-facing endpoints, with endpoint-appropriate limits, safe client identity keys, and denial behavior that does not expose sensitive information.
5. Update the OAuth threat model and interoperability/security test suite for the standard MCP transport, durable operation lifecycle, registration/redirect rejection, consent persistence, code confidentiality, configured lifetimes, throttling, PKCE, rotation, revocation, scope, ownership, and role boundaries.

## Done when

- A conforming MCP client can discover and invoke only the registered tools through the standard transport as its concrete OAuth user.
- MCP operations survive restart, are idempotent and auditable, and cannot bypass shared services or worker boundaries.
- OAuth redirect, consent, authorization-code, lifetime, and abuse-control paths have automated negative coverage and an updated threat model.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-03 | Jules / `jules-799593014151799790-53fa79f4` — Registered standard MCP server transport (`ModelContextProtocol.AspNetCore`) at `/mcp` with Bearer authorization and `mcp:access` scope validation via `IMcpActorResolver`. Annotated domain tools, retired bespoke `Mcp*Controllers`, created ADR 0004. Verification: `dotnet test ElixomClaim.slnx` passed 135 tests (94 Lib, 41 Web). Affected files: `Program.cs`, `adr/0004-standard-mcp-transport-and-rest-adapter-retirement.md`, `Mcp/IMcpActorResolver.cs`, `Mcp/McpActorResolver.cs`, `Mcp/Tools/`, `Controllers/Mcp*.cs` (deleted), `McpStandardTransportIntegrationTests.cs`. |
| 2 | Complete | 2026-09-03 | Jules / `jules-14241104565592229238-78b425ed` — Replaced process-local operation tracking with durable `dbclaim.OperationRecords` table, EF migration `20260903100000_AddOperationRecordsTable`, `IOperationRecordService`/`OperationRecordService`, and refactored `OperationsTools`. Verification: `dotnet test ElixomClaim.slnx` passed 134 tests (96 Lib, 38 Web). Affected files: `src/ElixomClaim.Lib/Entities/OperationRecord.cs`, `src/ElixomClaim.Lib/Data/ApplicationDbContext.cs`, `src/ElixomClaim.Lib/Migrations/20260903100000_AddOperationRecordsTable.cs`, `src/ElixomClaim.Lib/Services/IOperationRecordService.cs`, `src/ElixomClaim.Lib/Services/OperationRecordService.cs`, `src/ElixomClaim.Lib/DependencyInjection.cs`, `src/ElixomClaim.Web/Mcp/Tools/OperationsTools.cs`, `src/ElixomClaim.Lib.Tests/Services/OperationRecordServiceTests.cs`, `src/ElixomClaim.Web.Tests/Controllers/McpToolBoundaryTests.cs`. |
| 3 | Complete | 2026-09-03 | Jules / `jules-oauth-hardening` — Implemented configured `OAuthOptions` token lifetimes, strict dynamic registration admission policy and redirect URI shape validation, `OAuthConsent` entity and persistence in `dbclaim.OAuthConsents`, EF Core migration `20260903110000_OAuthHardeningAndConsents`, non-retention of raw authorization codes, and parameter revalidation on `POST /oauth/authorize`. Verification: `dotnet test ElixomClaim.slnx` passed 139 tests (100 Lib, 39 Web). Affected files: `src/ElixomClaim.Lib/Entities/OAuthEntities.cs`, `src/ElixomClaim.Lib/Data/ApplicationDbContext.cs`, `src/ElixomClaim.Lib/Services/IOAuthService.cs`, `src/ElixomClaim.Lib/Services/OAuthService.cs`, `src/ElixomClaim.Lib/Migrations/20260903110000_OAuthHardeningAndConsents.cs`, `src/ElixomClaim.Web/Controllers/OAuthController.cs`, `src/ElixomClaim.Lib.Tests/Services/OAuthServiceTests.cs`, `src/ElixomClaim.Web.Tests/Controllers/McpOAuthSecurityTests.cs`, `src/ElixomClaim.Web.Tests/Authentication/BearerTokenAuthenticationHandlerTests.cs`. |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
