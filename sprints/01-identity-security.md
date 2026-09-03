# Sprint 01 — Identity, Authorization, Audit, and OAuth Foundation

## Ordered backlog

1. Model `User`, hierarchical role enum, active/blocked state, profile/bank fields, unique normalized email, and safe bootstrap-admin seeding.
2. Configure Google OpenID Connect sign-in/cookie lifecycle without a Workspace-domain restriction; reject unknown/inactive users with a clear not-provisioned experience; refresh role claims at login.
3. Add policy/ownership services and test every role boundary, including Blocked access and inherited capabilities.
4. Model append-only audit records and implement a redacting audit service with actor, correlation, IP, action, target, before/after, and MCP flag.
5. Build the in-house OAuth authorization-server foundation: dynamic client registration with client-authentication policy, registered-client lifecycle, exact redirect URI validation, consent, authorization code storage, PKCE S256 verification, short-lived tokens, refresh rotation/revocation, scopes, throttling, and audit events. Use platform cryptographic primitives; complete the ADR/threat model before code.
6. Implement bearer-token validation middleware that projects the resolved application user into `HttpContext.User`; test expired, revoked, wrong-client, and scope/role failure paths.
7. Add Admin user management and audit views: Administrator sees all permitted audit data; Manager sees operational audit plus email recipient/subject/status/sent-date metadata only, never email body or bank details.
8. Add authorization query/projection tests proving Managers cannot retrieve email body/bank details through direct routes, exports, logs, or APIs.

## Done when

- An allow-listed active Google user can sign in; an unprovisioned or blocked identity cannot.
- OAuth and application security events are auditable without secrets, and authorization tests demonstrate no privilege escalation.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-02 | User entity, UserRole enum, ApplicationDbContext mapping with unique index on NormalizedEmail, AddUserEntity migration, and SeedBootstrapAdminAsync. Command: `dotnet test ElixomClaim.slnx` passed (45 tests total). |
| 2 | Complete | 2026-09-02 | Google OIDC & Cookie authentication, UserValidationEvents principal validation, AccountController, Login/AccessDenied views. Command: `dotnet test ElixomClaim.slnx` passed (48 tests total). |
| 3 | Complete | 2026-09-03 | Reconciliation verified shared minimum-role and ownership handlers, the Active User/Teller/Manager/Accountant/Administrator policies, Blocked denial, and inherited role boundaries. Files: `src/ElixomClaim.Lib/Authorization/AuthorizationHandlers.cs`, `PolicyNames.cs`, `DependencyInjection.cs`, `src/ElixomClaim.Lib.Tests/Authorization/AuthorizationTests.cs`. Command: `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore --filter FullyQualifiedName~AuthorizationTests` passed (20). |
| 4 | Blocked | 2026-09-03 | Redaction, actor/correlation/IP/MCP metadata, and insert persistence are implemented (`AuditServiceTests`), but `dbclaim.AuditRecords` has no database-level immutability protection against UPDATE/DELETE. This does not meet the required append-only audit invariant. Complete prerequisite 4a, add relational-persistence coverage, then rerun the audit suite. |
| 4a | In progress | 2026-09-03 | `/root` — enforce `AuditRecords` append-only behavior at the Azure SQL boundary, with a migration, verification, and operational documentation. Affected areas: Lib migrations, audit persistence tests, ADR/MEMORY. Prerequisite for item 4. |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
| 7 | Not started | — | — |
| 8 | Not started | — | — |
