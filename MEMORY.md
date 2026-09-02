# Project Memory

## Current baseline

- **Stage:** Sprint 00 Foundation complete; Sprint 01 Identity & security in progress.
- **Runtime:** .NET 10 / C# 14, ASP.NET Core MVC, EF Core, Azure SQL.
- **Database:** single-company Azure SQL database using schema `dbclaim`; money uses `decimal(18,2)`, JMD only, exact two-decimal storage/calculation with no additional rounding, and persisted instants are UTC.
- **Projects:** `ElixomClaim.Lib`, `ElixomClaim.Web`, and matching Lib/Web xUnit test projects under `src/`.
- **Frontend:** Razor MVC with Bootstrap 5.3 and jQuery 3.7 from CDN only; printable documents are HTML/CSS only—PDF generation is forbidden.

## Security baseline

- Browser sign-in is Google OpenID Connect and requires a pre-provisioned active user. A configured default-admin email is the bootstrap escape hatch.
- MCP is authenticated through the in-house OAuth 2.0 authorization server using dynamic client registration, Authorization Code + PKCE S256, consent, rotation/revocation, and audit trails. Calls execute as the real user and never receive an elevated MCP role.
- Mutations, OAuth security events, and MCP calls require append-only audit records. Logs/audit data must not contain credentials, access tokens, or unredacted bank data.

## Domain commitments

- Roles are hierarchical single roles: Blocked, User, Teller, Manager, Accountant, Administrator.
- Claims soft-delete and cannot be changed once accepted or rejected.
- A job payment has either a claimant or a collection client—not both—and only changes line items while Processing.
- Marking a job Paid atomically updates linked claims, collections, and payrolls and creates an idempotent notification outbox record.
- Salary generation and payment state changes are domain services called by thin hosted services/controllers/MCP tools.
- MCP tools are grouped by domain class and include constrained email composition/outbox dispatch and audited background-operation requests; they do not provide arbitrary email or direct worker execution.
- Paid job payments are immutable; corrections are separately auditable linked reversal/adjustment payments.
- Accountant and Administrator can see bank details and email bodies; Manager sees email metadata only.
- Financial, audit, and email records retain for nine years. The configured retention floor is four years.

## Delivery status

Agents must use the per-sprint `Progress` table as the item-level reservation and evidence log. This table records sprint-level state only; update it when a sprint starts or completes, or when its active item/blocker changes.

| Sprint | State | Note |
| --- | --- | --- |
| 00 Foundation | Complete | All 8 items complete. See `sprints/00-foundation.md`. |
| 01 Identity & security | Complete | All 8 items complete. See `sprints/01-identity-security.md`. |
| 02 Claims | Complete | All 5 items complete. See `sprints/02-claims.md`. |
| 03 Clearing house | Planned | See `sprints/03-clearing-house.md`. |
| 04 Job payments | Planned | See `sprints/04-job-payments.md`. |
| 05 Salary & payroll | Planned | See `sprints/05-salary-payroll.md`. |
| 06 MCP & readiness | Planned | See `sprints/06-mcp-release.md`. |

## Open decisions / risks

1. **OAuth security review:** the in-house OAuth server requires a formal threat model, interoperability suite, and independent security review before release.
2. **Reversal accounting:** document the accounting treatment and authorization workflow for reversal/adjustment payments before Sprint 04 implementation.

## Decision log

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-09-02 | Treat README as the consolidated working specification; preserve `context/` as source history. | The source documents overlap and include unfinished fragments. |
| 2026-09-02 | Use a durable email outbox in addition to `EmailLogs`. | In-memory queues alone cannot guarantee reliable or non-duplicated financial notifications. |
| 2026-09-02 | Implement shared service authorization for MVC and MCP. | Ensures MCP identity inheritance cannot bypass business permissions. |
| 2026-09-02 | Use JMD as the sole currency and `decimal(18,2)` money. | Single-company operating model. |
| 2026-09-02 | Paid payments are immutable; corrections use linked reversal/adjustment records. | Protects financial history and auditability. |
| 2026-09-02 | Full bank/email-body access is Accountant/Administrator only; Managers see email metadata. | Least-privilege handling of sensitive financial and message data. |
| 2026-09-02 | Retain financial, audit, and email records for nine years; configuration may not go below four. | Business retention requirement. |
| 2026-09-02 | Build the OAuth 2.0 server in-house, including dynamic client registration. | Product requirement; see ADR 0001. |
| 2026-09-02 | Group MCP tools by domain and expose only constrained email/background-operation commands. | Keeps tool surface maintainable and prevents MCP from bypassing outbox, authorization, and audit controls. |
| 2026-09-02 | Permit any Google account that is on the active-user allow-list. | No Workspace-domain restriction. |
| 2026-09-02 | Restrict Collection Client configuration to Administrators. | Centralized control of client options, assignments, and bank details. |
| 2026-09-02 | Preserve JMD values to two decimal places with no additional rounding. | Business requirement. |
| 2026-09-02 | Apply Jamaican law to privacy/legal requirements. | Business requirement. |
| 2026-09-02 | Use `privacy@elixom.com` as the privacy and support contact. | Published privacy/support contact. |
