# Elixom Claim Management

Elixom Claim is a secure, auditable claims and payment-operations system for employees, contractors, tellers, managers, accountants, and administrators. It combines four connected workflows—claims, payment collections, job payments, and recurring payroll—into one ASP.NET Core MVC application.

> **Status:** design and delivery plan. The implementation has not yet been scaffolded. Work in the ordered [`sprints/`](sprints) backlog.

## Product outcomes

| Area | Outcome |
| --- | --- |
| Claims | People submit, track, amend, and discuss their own claims. |
| Clearing house | Tellers record client collections and issue accessible, printable HTML receipts. |
| Payments | Managers assemble payable work; accountants schedule and settle it with a traceable payout record. |
| Payroll | Recurring salaries generate controlled payrolls and become payable job payments. |
| Accountability | Every sensitive action is attributable to the authenticated person, including MCP-assisted actions. |

## Architecture

The solution targets **.NET 10 / C# 14**, ASP.NET Core MVC, EF Core, and Azure SQL Server. Domain behavior belongs in a reusable library; the web project is the delivery layer.

```text
src/
├── ElixomClaim.Lib/             Domain entities, EF Core, services, DTOs, DI extensions
├── ElixomClaim.Web/             MVC UI, authentication, OAuth endpoints, MCP transport, hosted services
├── ElixomClaim.Lib.Tests/       Unit tests for domain rules and services
└── ElixomClaim.Web.Tests/       Integration tests for web, auth, authorization, and endpoints
```

All database objects use the Azure SQL schema **`dbclaim`**. EF migrations are applied intentionally at application startup through a guarded `ApplyDatabaseMigrationsAsync()` extension; production deployment must use a single migration runner/instance to avoid concurrent migration races.

### Layering rules

- `ElixomClaim.Lib` owns entities, the `DbContext`, transaction-aware business services, validation, email composition contracts, and shared authorization-aware operations. It must not depend on MVC controllers or Razor views.
- `ElixomClaim.Web` owns HTTP, Razor UI, Google sign-in wiring, OAuth endpoint plumbing, MCP transport, and thin hosted-service schedulers.
- Controllers and MCP tools call the same Lib services. Neither is allowed to reimplement authorization or state-transition rules.
- This is a single-company application; do not add tenant identifiers or tenant-resolution infrastructure.
- Use `decimal(18,2)` for money, **JMD** as the sole currency, UTC timestamps for persisted instants, and a single identifier convention consistently. Preserve exact two-decimal values; do not introduce intermediate, display, or payout rounding beyond the database scale.

## Identity, roles, and authorization

Human users sign in with Google OpenID Connect only. There is no local password flow and no Google Workspace-domain restriction: any Google account may sign in when its email matches an active `dbclaim.Users` record. A configured `Authentication:DefaultAdminEmail` is seeded/promoted safely so the system cannot be locked out.

Roles are stored as one hierarchical application role, not a collection of unrelated permissions. A higher role inherits lower-role capabilities except that `Blocked` has no access.

| Role | Effective capability |
| --- | --- |
| Blocked | No authenticated application access. |
| User | Own claims, profile, bank details, and own payment history. |
| Teller | User capabilities plus Payment Clearing House. |
| Manager | Teller capabilities plus claims, collections, job-payment management, and operational audit visibility. |
| Accountant | Manager capabilities plus salaries, payroll, scheduling, and marking payments paid. |
| Administrator | Full access, including user/client configuration and all audit logs. |

Use policies (for example, `CanCollectPayments`, `CanManageClaims`, `CanManagePayroll`, `CanExecutePayments`) instead of scattering role strings. Ownership checks remain mandatory even after an endpoint has passed a role policy.

Accountants and Administrators may view full bank details and stored email bodies. Managers may see only email-list metadata: recipient, subject, status, and sent date. Apply these restrictions in query projections as well as the UI—do not load sensitive content merely to hide it.

## Domain and lifecycle rules

### Claims

A claim has a claimant, title, description, date of job, total claimed, creation timestamp, workflow status (`Draft`, `Submitted`, `Accepted`, `Rejected`), payment status, and soft-delete fields. Claimants can edit or soft-delete only their own claim before it is accepted; all reads exclude soft-deleted records by default. Comments are append-only and either public or management-private.

```text
Draft ──submit──> Submitted ──accept──> Accepted ──attach to job──> Processing ──job paid──> Honoured
                         └──reject──> Rejected
```

### Collections and receipts

A teller records a collection against a `CollectionClient`: payor details, Administrator-configured purpose and amount options, collection method (`Cash`, `Pos`, `BankTransfer`, `CreditNote`), payment date, and internal processing fee. On confirmation, persist the collection, queue the responsive HTML receipt to the payor (when supplied), client recipients, and configured system-copy address, and expose a printable HTML route. Never generate PDFs.

Collections may only move forward:

```text
Collected ──attached to compatible job──> Processing ──job paid──> Transferred
```

Only `Collected` collections can be attached. A job containing collections has one and only one collection client.

### Job payments

A job payment groups one payee type: either a claimant/user or a collection client, never both. It contains claims, collections, payrolls, deductions, calculated totals, descriptive/public and internal notes, destination bank details, and payout metadata.

`TotalPaid = JobTotal − ClientProcessingFee − TotalTxnProcessingFee − TotalDeductions`.

Line items and deductions are editable only in `Processing`. The status flow is:

```text
Processing ──submit──> Submitted ──accountant schedules──> Scheduled ──date + transaction no.──> Paid
```

Marking a job paid is an atomic domain operation: it records the payment details, marks attached payrolls `Paid`, collections `Transferred`, and claims `Honoured`, then queues the HTML payout summary. The summary includes recipient and bank information, totals, and itemized claims, collections, and deductions. Retries must not duplicate the business transition or receipt; use an outbox/idempotency key.

Paid job payments are immutable. Corrections use a separately auditable reversal or adjustment job payment linked to the original; never edit or delete a paid financial record.

### Salary and payroll

Salary definitions hold the user, base amount, description, active/start/end bounds, first/last salary date, monthly and daily recurrence, nearest weekday, and benefit/deduction adjustments. The daily scheduler delegates to a testable salary engine; it does not contain calculation logic.

For each active definition, calculate `lastSalaryDate + recurrenceMonths + recurrenceDays`, adjust to the configured nearest weekday using a documented deterministic tie-break, then generate only when the due date is inside the inclusive start/end range and has not already generated a payroll. Update `LastSalaryDate` in the same transaction.

Payrolls are generated only from salary definitions. Entries are ordered: base first, benefits, deductions, then custom entries. Generated entries are locked. Before submission, custom entries may be added, but negative entries cannot reduce net pay below zero. Submitting locks the payroll and creates its bound `Processing` job payment.

## Security, OAuth, and MCP

The web project contains an in-house OAuth 2.0 authorization server for MCP clients—no external authorization-server product/library is the protocol implementation. It supports dynamic client registration, authorization code flow with mandatory PKCE S256, validated redirect URIs, user consent, short-lived access tokens, securely stored/rotated refresh tokens, revocation, scope checks, and abuse protections. Endpoint names are `/oauth/register`, `/oauth/authorize`, `/oauth/token`, `/oauth/revoke`, and the MCP transport endpoint (for example `/mcp/sse`). Use platform cryptographic primitives and constant-time comparisons; do not invent cryptographic algorithms or expose client secrets in source control.

MCP authentication resolves the token to the concrete `User` record and projects that identity and role claims into `HttpContext.User`. An MCP tool has no elevated pseudo-role: it invokes the same domain service and policies as the web UI. Log every token/security event and every MCP invocation with actor, action, target, time, IP/correlation information, and `IsMcp = true`.

MCP tools are grouped into related class files under `ElixomClaim.Web/Mcp/Tools/`, rather than one monolithic server class: `ClaimTools`, `CollectionTools`, `JobPaymentTools`, `PayrollTools`, `EmailTools`, and `OperationsTools`. Tool classes are HTTP/MCP adapters only and call Lib services; tool schemas use explicit request/response DTOs and never expose entities, credentials, tokens, or unrestricted database queries.

`EmailTools` can compose approved receipt/payment-summary templates and return a redacted preview, then queue a send through the durable outbox. It cannot send arbitrary free-form content or add arbitrary recipients. `OperationsTools` may request safe, idempotent background work—not execute worker internals directly: Accountants can request salary-generation preview/run; Administrators can request an outbox dispatch wake-up or other approved maintenance command. Each command has an idempotency key, operation record, authorization/scope check, audit entry, and observable status. Destructive maintenance, credential/key operations, retention purge, and direct bulk email are not MCP tools.

## Notifications, audit, and observability

- Queue emails through a durable outbox and background worker. `IEmailSender` implementations support SMTP and Azure Communication Services, selected by configuration.
- Persist every composed/send attempt in `EmailLogs`: addressing, subject, HTML body, provider, relation, attempt/status, timestamps, and safe failure metadata. Do not store credentials in this table or logs. A missing or invalid optional payor email skips only that recipient; client and system-copy receipts continue and the skipped-recipient outcome is recorded.
- Persist audit events for mutations, access/security events, state changes, role changes, OAuth issuance/revocation, and MCP operations. Include before/after JSON where safe, but redact secrets and sensitive tokens.
- Inject `ILogger<T>` into controllers, services, and hosted services. Log structured identifiers and outcome—not credentials, access tokens, bank-account numbers, or email bodies.
- Retain financial records, audit logs, and email records for **nine years**. Make retention configurable, but never permit less than **four years**; expiry/purge must be auditable and legally reviewed.

## Frontend experience

The interface is server-rendered Razor plus **Bootstrap 5.3 and jQuery 3.7 from CDN only**; do not commit local copies to `wwwroot`. Use Subresource Integrity where provided and a purposeful inline SVG favicon. The intended experience is calm and task-focused:

- Put each role’s next action and work queue first; show counts and state, not decorative dashboards.
- Use explicit status badges paired with text, clear empty states, and filters that retain their selection.
- Present payment and receipt details in print-friendly, responsive HTML with `@media print`; internal notes never appear in print or email.
- Build semantic forms with labels, help/error text, keyboard support, focused validation summaries, and high-contrast state indicators.
- Provide a real privacy page linked from the footer, describing Google sign-in, financial/contact data, email delivery, audit retention, and user rights under the laws of Jamaica. The privacy and support contact is `privacy@elixom.com`. Legal review is required before production launch.

## Configuration

Secrets belong in user secrets, Azure Key Vault, or deployment configuration—not `appsettings.json` committed to the repository. Expected configuration groups include:

```json
{
  "ConnectionStrings": { "ClaimDatabase": "<Azure SQL connection string>" },
  "Authentication": {
    "Google": { "ClientId": "<client id>", "ClientSecret": "<secret>" },
    "DefaultAdminEmail": "admin@example.com"
  },
  "Notifications": {
    "Provider": "Acs",
    "FromAddress": "no-reply@example.com",
    "SystemCopyAddress": "operations@example.com"
  }
}
```

## Delivery plan

The implementation order and definition of done live in [`sprints/README.md`](sprints/README.md). Start with the foundation and security boundaries before building dashboards or email templates. The backlog intentionally makes production hardening and accessibility/release checks explicit rather than treating them as follow-up work.

GitHub Actions is the required CI/CD platform. The delivery backlog covers build/test gates, security scanning, deployment environments, migration execution, and protected release workflow before production release.

## Project working agreements

Read [`AGENTS.md`](AGENTS.md) before changing the codebase and update [`MEMORY.md`](MEMORY.md) when an enduring decision, implementation fact, or unresolved risk changes. Record significant architecture choices in [`adr/`](adr/). The source documents remain in [`context/`](context/) as input history; this README is the consolidated working specification.
