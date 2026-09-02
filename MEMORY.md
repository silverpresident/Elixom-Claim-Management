# Project Memory

## Current baseline

- **Stage:** specification and delivery planning; no .NET solution has been scaffolded yet.
- **Runtime:** .NET 10 / C# 14, ASP.NET Core MVC, EF Core, Azure SQL.
- **Database:** default schema `dbclaim`; money uses `decimal(18,2)` and persisted instants are UTC.
- **Projects:** `ElixomClaim.Lib`, `ElixomClaim.Web`, and matching Lib/Web xUnit test projects under `src/`.
- **Frontend:** Razor MVC with Bootstrap 5.3 and jQuery 3.7 from CDN only; printable documents are HTML/CSS only—PDF generation is forbidden.

## Security baseline

- Browser sign-in is Google OpenID Connect and requires a pre-provisioned active user. A configured default-admin email is the bootstrap escape hatch.
- MCP is authenticated through the application’s OAuth 2.0 authorization server using Authorization Code + PKCE S256. Calls execute as the real user and never receive an elevated MCP role.
- Mutations, OAuth security events, and MCP calls require append-only audit records. Logs/audit data must not contain credentials, access tokens, or unredacted bank data.

## Domain commitments

- Roles are hierarchical single roles: Blocked, User, Teller, Manager, Accountant, Administrator.
- Claims soft-delete and cannot be changed once accepted or rejected.
- A job payment has either a claimant or a collection client—not both—and only changes line items while Processing.
- Marking a job Paid atomically updates linked claims, collections, and payrolls and creates an idempotent notification outbox record.
- Salary generation and payment state changes are domain services called by thin hosted services/controllers/MCP tools.

## Delivery status

Agents must use the per-sprint `Progress` table as the item-level reservation and evidence log. This table records sprint-level state only; update it when a sprint starts or completes, or when its active item/blocker changes.

| Sprint | State | Note |
| --- | --- | --- |
| 00 Foundation | Planned | See `sprints/00-foundation.md`. |
| 01 Identity & security | Planned | See `sprints/01-identity-security.md`. |
| 02 Claims | Planned | See `sprints/02-claims.md`. |
| 03 Clearing house | Planned | See `sprints/03-clearing-house.md`. |
| 04 Job payments | Planned | See `sprints/04-job-payments.md`. |
| 05 Salary & payroll | Planned | See `sprints/05-salary-payroll.md`. |
| 06 MCP & readiness | Planned | See `sprints/06-mcp-release.md`. |

## Open decisions / risks

1. **OAuth implementation:** confirm the approved library/security review approach for the custom authorization server before implementation; do not hand-roll cryptography or token validation.
2. **Google tenancy:** confirm whether sign-in is restricted to a Google Workspace domain or permits any Google account already on the allow-list.
3. **Client configuration ownership:** confirm whether only Administrators manage collection-client purpose/amount options and client-user assignments.
4. **Audit scope:** Managers need operational audit visibility; exact limits for user/security audit visibility should be confirmed during Sprint 01.
5. **Retention/legal:** confirm data-retention periods, privacy contact, and jurisdiction before publishing the production privacy policy.

## Decision log

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-09-02 | Treat README as the consolidated working specification; preserve `context/` as source history. | The source documents overlap and include unfinished fragments. |
| 2026-09-02 | Use a durable email outbox in addition to `EmailLogs`. | In-memory queues alone cannot guarantee reliable or non-duplicated financial notifications. |
| 2026-09-02 | Implement shared service authorization for MVC and MCP. | Ensures MCP identity inheritance cannot bypass business permissions. |
