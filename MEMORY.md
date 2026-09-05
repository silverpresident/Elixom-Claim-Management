# Project Memory

## Current baseline

- **Stage:** Sprint 08 MCP Transport & OAuth Hardening in progress (Sprint 08 Item 3 Complete).
- **Runtime:** .NET 10 / C# 14, ASP.NET Core MVC, EF Core, Azure SQL.
- **Database:** single-company Azure SQL database using schema `dbclaim`; money uses `decimal(18,2)`, JMD only, exact two-decimal storage/calculation with no additional rounding, and persisted instants are UTC.
- **Audit Immutability:** `dbclaim.AuditRecords` append-only trigger `TR_AuditRecords_PreventMutation` enforced at Azure SQL boundary via migration `20260903090000_AddAuditRecordAppendOnlyTrigger` and ADR 0003.
- **MCP Transport:** Standard .NET MCP Server transport (`ModelContextProtocol.AspNetCore` 2.2.0) registered at `/mcp` with mandatory Bearer authentication (`BearerTokenAuthenticationHandler`) and `mcp:access` scope validation via `IMcpActorResolver`. Legacy bespoke `/mcp/*` REST controllers retired per ADR 0004. Domain-scoped tool classes (`ClaimTools`, `CollectionTools`, `JobPaymentTools`, `PayrollTools`, `EmailTools`, `OperationsTools`) are annotated with `[McpServerToolType]` and `[McpServerTool]`.
- **Durable MCP Operation Tracking:** MCP operations and idempotency keys are durably stored in the `dbclaim.OperationRecords` table via `IOperationRecordService` and migration `20260903100000_AddOperationRecordsTable`, ensuring operation tracking survives application restarts.
- **Collections schema:** `CollectionClients`, client-user assignments, client bank details, client-scoped purpose/amount options, and `CollectionTransactions` are in the `dbclaim` schema. Composite foreign keys prevent a transaction from pairing options with a different client. See `20260902214419_AddCollectionEntities`.
- **Collection configuration:** only the shared `CollectionClientAdministrationService` may create/configure clients, assignments, options, and bank details; it requires an active Administrator and emits redacted audit events. The MVC adapter is `/admin/collection-clients`.
- **Collection recording:** `CollectionService` requires an active teller or above, validates active options against the selected active client, and persists the JMD collection, receipt queue records, and audit record atomically on relational providers. `EmailOutboxItems` (`20260902214751_AddEmailOutbox`) has unique idempotency keys and status scheduling fields; delivery is the next sprint item.
- **Outbox delivery:** `EmailOutboxItems` are dispatched through SMTP, Azure Communication Services, or the development fake sender. `EmailLogs` (`20260902215402_AddEmailLogs`) record every delivery/skipped outcome; failures retry with bounded exponential backoff and invalid optional payor addresses are recorded as skipped without blocking other recipients.
- **Teller collections UI:** `/collections` provides the recording teller’s 24-hour queue, entry, review, controlled reissue, and print-ready HTML receipt. Print/email receipts do not include internal processing fees.
- **Retention configuration:** `Retention:FinancialRecordRetentionYears` defaults to nine and is validated at startup with a four-year minimum.
- **Database baseline:** EF history is deliberately reset for this first implementation. `20260903053340_InitialCreate` is now the sole migration and creates every current `dbclaim` object.
- **Salary and payroll schema:** `SalaryDefinitions`, typed percentage/fixed `SalaryAdjustments`, salary-backed `Payrolls` with unique due-period identity, and ordered `PayrollEntries` are available. Payrolls require their source salary definition; see `20260903053340_InitialCreate` and `SalaryPayrollModelTests`.
- **Salary recurrence:** `SalaryRecurrencePlanner` is the shared, pure source of due-date eligibility. It adds months then days to `LastSalaryDate`, picks the nearest configured weekday (earlier occurrence on a tie), applies inclusive definition bounds, and suppresses existing due periods.
- **Payroll generation:** `SalaryPayrollService` requires Accountant authority and atomically creates salary-sourced generated payrolls with locked base/benefit/deduction entries, an exact total, an advanced cursor, and an audit record.
- **Payroll submission:** Accountants may add custom entries only while a payroll is generated and unlocked; negative additions cannot make net pay negative. Submission locks every entry and creates exactly one linked Processing job payment.
- **Payroll operations:** Daily generation, the Accountant Generate Now action, and constrained OAuth/MCP preview/run adapters all delegate to `ISalaryPayrollService`; unique salary due periods provide idempotency across scheduler instances.
- **Salary & payroll UI:** Accountants use `/payroll` for salary-definition creation, service-backed due/total previews, lifecycle actions, and scoped payroll audit history.
- **Job management:** `JobPaymentService` is the shared Manager+ adapter for Processing-only job creation and line management. It validates compatible source ownership/state and recalculates all stored JMD totals server-side.
- **Job lifecycle:** Managers submit valid Processing payments; only Accountants can schedule Submitted payments at a UTC time. Scheduled jobs are immutable because all line commands require Processing status.
- **Settlement:** only Accountants can mark a Scheduled job paid; it atomically records payment metadata, cascades claims/collections/payrolls to their paid states, queues payout notification records, and writes an audit event.
- **Accountant queue:** `/job-payments/accountant-queue` exposes Submitted and Scheduled payments for Accountant action; lifecycle, totals, source compatibility, and settlement cascade behavior are covered by focused tests.
- **Adjustments:** ADR 0002 is implemented through linked adjustment job payments: Accountant creation with a reason, Administrator approval, and Accountant settlement. Negative adjustments are recovery receivables and original paid records remain immutable.
- **Projects:** `ElixomClaim.Lib`, `ElixomClaim.Web`, and matching Lib/Web xUnit test projects under `src/`.
- **Frontend:** Razor MVC with Bootstrap 5.3 and jQuery 3.7 from CDN only; printable documents are HTML/CSS only—PDF generation is forbidden.
- **Development testing:** when and only when the host environment is Development and `DevelopmentTesting:Enabled` is true, the application uses a named EF Core InMemory database, seeds deterministic non-sensitive records for every implemented area, and exposes a role-selectable test sign-in for active roles. The Blocked sample account is inactive and cannot sign in. See `sprints/07-development-testing.md` and `src/ElixomClaim.Web/Development/DevelopmentDataSeeder.cs`.

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
| 01 Identity & security | Complete | All 8 items complete (including prerequisite 4a trigger immutability). See `sprints/01-identity-security.md`. |
| 02 Claims | Complete | All 5 items complete. See `sprints/02-claims.md`. |
| 03 Clearing house | Complete | All 6 items complete; build and 97 tests passed on 2026-09-02. See `sprints/03-clearing-house.md`. |
| 04 Job payments | Complete | All 8 items complete; verification recorded in `sprints/04-job-payments.md`. |
| 05 Salary & payroll | Complete | All ordered items complete; Lib/Web test evidence recorded in `sprints/05-salary-payroll.md`. |
| 06 MCP & readiness | Complete | All 9 items complete; build and 127 tests passed on 2026-09-03. See `sprints/06-mcp-release.md`. |
| 07 Development testing | Complete | Development-only in-memory sample data and role-selectable test login completed; full suite passed (130 tests) on 2026-09-03. See `sprints/07-development-testing.md`. |
| 08 MCP transport & OAuth hardening | In progress | Item 3 complete; standard MCP transport, durable operations, OAuth hardening, rate limiting, and threat-model/interoperability evidence. See `sprints/08-mcp-oauth-hardening.md`. |
| 09 Domain data completion | Planned | Required fields, all-`Guid` identifier conversion (ADR required first), mappings, migrations, and relational coverage. See `sprints/09-domain-data-completion.md`. |
| 10 Web workflow completion | Planned | Profile/dashboard, collection fields, job lifecycle/deductions, payroll adjustment/custom-entry workflows, and navigation. See `sprints/10-web-workflow-completion.md`. |
| 11 Deployment & release verification | Planned | Guarded production migration runner, refreshed development data, end-to-end coverage, and recorded release verification. See `sprints/11-deployment-and-release-verification.md`. |

## Open decisions / risks

1. **OAuth security review:** the in-house OAuth server requires a formal threat model, interoperability suite, and independent security review before release.
2. **2026-09-03 — Remediation delivery plan:** The attached task-list gaps are scheduled after the existing Sprint 01 audit prerequisite in Sprints 08–11. No task-list entry has been removed because no remediation was implemented in this planning change. Affected area: [sprints/08-mcp-oauth-hardening.md](sprints/08-mcp-oauth-hardening.md), [sprints/09-domain-data-completion.md](sprints/09-domain-data-completion.md), [sprints/10-web-workflow-completion.md](sprints/10-web-workflow-completion.md), [sprints/11-deployment-and-release-verification.md](sprints/11-deployment-and-release-verification.md).

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
| 2026-09-02 | Use linked partial/full accounting-only adjustments: Accountant creates, Administrator approves, Accountant settles; originals stay paid and immutable. | Approved reversal workflow; see [ADR 0002](adr/0002-reversal-adjustment-accounting.md). |
| 2026-09-03 | Enforce `AuditRecords` append-only trigger `TR_AuditRecords_PreventMutation` at Azure SQL boundary via EF migration `20260903090000_AddAuditRecordAppendOnlyTrigger` and ADR 0003. | Satisfies non-negotiable append-only audit invariant. |
| 2026-09-03 | Standardize MCP transport using `ModelContextProtocol.AspNetCore` mapped at `/mcp` with Bearer auth and `mcp:access` scope validation. Retire bespoke `/mcp/*` REST controllers per ADR 0004. | Compliance with standard MCP server specification and interoperability with conforming MCP client agents. |
| 2026-09-03 | Persist OAuth consents in dbclaim.OAuthConsents, enforce strict redirect URI shape/scheme rules during dynamic client registration and authorization, stop retaining raw authorization codes in database, and revalidate client and redirect URI on consent POST. | OAuth 2.0 hardening requirements under Sprint 08 Item 3. |
