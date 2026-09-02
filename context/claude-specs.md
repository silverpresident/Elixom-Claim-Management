# Elixom Claim — Technical Specification

## 1. Overview

Elixom Claim is  a modern C#/.NET solution. The system manages employee/contractor **claims**, third‑party **collections** (payments taken in on behalf of clients), consolidated **job
payments**, **payroll**, and recurring **salaries**, plus a cashier‑style
**Payment Clearing House** for tellers.

### 1.1 Goals

- Rebuild on **.NET 10**, C#, ASP.NET Core MVC.
- Clean separation between a shared **Lib** project (domain, data access,
  services) and a **Web** project (UI, controllers, MCP/OAuth2 endpoints).
- Azure SQL Server, custom schema `dbclaim`.
- Google SSO–only authentication; authorization is provisioned, not
  self‑service (see §4).
- Full audit trail for business actions and for MCP/AI activity.
- Expose an MCP server, secured by a full custom OAuth2.0 authorization
  server built into the Web project.

### 1.2 Out of scope / assumptions

- No PDF generation — all printable output (receipts, job payment details) is
  HTML designed to print cleanly.
- No existing PHP codebase to reverse-engineer against; behavior is derived
  entirely from the functional requirements below. Where the original PHP
  system may have had additional nuance not captured here, flag it for
  review rather than guessing.

---

## 2. Solution structure

```
/src
  /ElixomClaim.Lib
    /Entities
    /Data                 (DbContext, migrations, schema config)
    /Services
    /Extensions
    /DependencyInjection
  /ElixomClaim.Lib.Tests
  /ElixomClaim.Web
    /Controllers
    /Areas
      /Admin
      /Manager
      /Teller
      /Accountant
    /Views
    /Mcp                  (MCP server + tool definitions)
    /OAuth                (OAuth2 authorization server)
    /wwwroot
  /ElixomClaim.Web.Tests
AGENTS.md
MEMORY.md
```

- `ElixomClaim.Lib` — central library: entities, `ElixomClaimDbContext`
  (schema `dbclaim`), services, DI registration extensions
  (`AddElixomClaimLib(...)`), no ASP.NET dependency beyond what's needed for
  DI abstractions.
- `ElixomClaim.Web` — MVC controllers/views, Google SSO auth, MCP endpoint,
  OAuth2 authorization server, background hosted services.
- Remove the default `Class1.cs` scaffold file from both projects.
- Each project gets a matching xUnit test project.

---

## 3. Tech stack & infrastructure

| Concern | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core MVC |
| Database | Azure SQL Server, schema `dbclaim` |
| ORM | EF Core (SQL Server provider), `HasDefaultSchema("dbclaim")` |
| Auth (users) | Google SSO only (OpenID Connect), no local password login |
| Auth (MCP clients) | Custom OAuth2.0 authorization server hosted in `ElixomClaim.Web` |
| Frontend libs | Bootstrap + jQuery served **from CDN only** — never bundled locally |
| Background jobs | `IHostedService` implementations (no Hangfire/Quartz) |
| Email transport | SMTP **and** Azure Communication Services (ACS) Email — pluggable, configured per environment |
| Logging | `ILogger<T>` injected into every controller, service, and hosted service |
| Icon | Generated SVG favicon (no external image asset) |

### 3.1 Email sending

- `IEmailSender` abstraction in Lib with two implementations:
  `SmtpEmailSender` and `AcsEmailSender`, selected via configuration
  (`Email:Provider = Smtp | Acs`).
- Every composed/sent email is persisted to an `EmailLog` table regardless of
  provider: `To`, `From`, `Cc`, `Bcc`, `Subject`, `BodyHtml`, `SentAtUtc`,
  `Provider`, `Status` (Sent/Failed), `RelatedEntityType`,
  `RelatedEntityId`.
- A "system copy" recipient address is configurable in app settings and is
  CC'd/BCC'd on relevant notifications (e.g. Teller receipts).

### 3.2 Background/recurring work (IHostedService)

- `SalaryGenerationHostedService` — runs on a timer (e.g. daily), scans
  active `SalaryDefinition`s whose computed next due date has arrived, and
  generates the corresponding `Payroll` via the salary engine (§8.5).
- Hosted services should be thin schedulers that call into Lib services
  (`ISalaryEngine`, etc.) so the actual logic is testable outside of the
  hosting infrastructure.

---

## 4. Authentication & authorization

### 4.1 Sign-in model

- All human users sign in via **Google SSO** (OpenID Connect) — there is no
  username/password login for the app itself.
- A user must already exist in the `Users` table (added by an Administrator
  via the admin interface, keyed by email) before they can successfully sign
  in. Signing in with an email not present in the table is rejected (shown a
  "not provisioned" page), not auto-provisioned.
- **Bootstrap admin**: `appsettings` may specify a `DefaultAdminEmail`. On
  sign-in, if the authenticated Google email matches this setting, the user
  is treated as a full Administrator even if not yet present in the `Users`
  table (and should be auto-created/promoted on first login so the system is
  never lockable).

### 4.2 Roles

Single role per user (simplest reading of the spec — flag if multiple
concurrent roles are actually needed):

| Role | Access |
|---|---|
| Blocked | No access (default state for a disabled account) |
| User | Create/manage own claims, manage own profile & bank info |
| Teller | Payment Clearing House |
| Manager | Claims management + Teller features |
| Accountant | Manager features + Payroll/Salary |
| Administrator | Everything, incl. user management |

- Implemented as ASP.NET Core `[Authorize(Roles = "...")]` plus policy-based
  checks where a feature spans multiple roles (e.g. `CanManageClaims` policy
  satisfied by Manager, Accountant, Administrator).
- Role is stored on the `User` entity and refreshed into the auth cookie's
  claims on login.

### 4.3 MCP + OAuth2.0

- `ElixomClaim.Web` hosts a **full custom OAuth2.0 authorization server**
  (authorization code + refresh token flow at minimum) — not delegated to
  an external IdP. This issues tokens to MCP clients.
- The MCP server authenticates **as a specific application user** — i.e. an
  OAuth2 client is associated with (or, during the auth flow, obtains
  consent from) one `User` record, and the resulting token carries that
  user's identity and role. All MCP tool calls execute with that user's
  actual permissions — no elevated "MCP" pseudo-role.
- Every MCP tool invocation is written to the audit log (§9) with an
  `IsMcp = true` flag and the acting user, so MCP actions are distinguishable
  from and traceable alongside normal UI actions.
- Suggested initial MCP tools: list/read claims, submit a claim, list job
  payments, read payroll status — scoped to whatever the acting user's role
  already permits; the MCP layer must not bypass the role checks used by the
  MVC controllers (ideally both call the same Lib services).

---

## 5. Domain model

> All monetary fields use `decimal(18,2)`. All entities have `Id` (int or
> guid — pick one convention and use consistently), `CreatedAtUtc`, and
> soft-delete support where noted.

### 5.1 User & profile

**User**
- `Id`, `Email` (unique, used for Google SSO match), `DisplayName`, `Role`,
  `IsActive`
- Bank info: `BankAccountName`, `BankAccountNo`, `BankName`, `BankBranch`

### 5.2 Claim

**Claim**
- `Id`, `UserId` (claimant), `Title`, `Description`, `DateOfJob`,
  `TotalClaimed`, `CreatedAtUtc`, `Status` (`Draft`, `Submitted`,
  `Accepted`, `Rejected`), `IsDeleted` (soft delete), `DeletedAtUtc`
- `JobPaymentId` (nullable — set once added to a Job Payment)
- `PaymentStatus` derived/companion field distinct from workflow `Status`:
  when accepted and added to a job payment → `Processing`; when the job
  payment is paid → `Honoured`.
- Editable/deletable by the claimant **only while `Status = Draft` or
  `Submitted`** (not once `Accepted` or `Rejected`, per "cannot delete/edit
  if accepted" — reject edits/deletes server-side even if the UI hides the
  controls).

**ClaimComment**
- `Id`, `ClaimId`, `AuthorUserId`, `Body`, `CreatedAtUtc`, `IsPrivate`
  (private comments visible to management roles only; non-private comments
  are visible to the claimant too). Comments are an ordered, append-only
  list — no edit/delete requirement stated.

### 5.3 Collections (Payment Clearing House)

**CollectionClient** (the "payee" a teller collects on behalf of)
- `Id`, `Name`, `Notes`, `PerJobProcessingFee`, `PerTransactionFee`
- Bank info: `BankAccountName`, `BankAccountNo`, `BankName`, `BankBranch`
- `PurposeOptions` — configurable list of purpose-of-payment values scoped to
  this client
- `AmountOptions` — configurable list of preset/allowed amounts scoped to
  this client
- Assigned users (many-to-many `CollectionClientUser`) — users who can see
  this client's collections and job payments

**Collection**
- `Id`, `CollectionClientId`, `PayorName`, `PayorEmail` (optional),
  `PayorPhone` (optional), `Purpose`, `AmountCollected`, `Method` (`Cash`,
  `Pos`, `BankTransfer`, `CreditNote`), `PaymentDate` (defaults to now),
  `CollectedByUserId` (teller), `CreatedAtUtc`
- `TransferStatus`: `Collected` → `Processing` (added to a Job Payment) →
  `Transferred` (job payment paid). Can only be added to a Job Payment while
  `Collected`.
- `InternalProcessingFee` — per-collection fee (sourced from the client's
  `PerTransactionFee`), rolled up into the owning Job Payment's
  `TotalTxnProcessingFee`.
- `JobPaymentId` (nullable)
- A printable HTML receipt is generated from this record; the receipt (as an
  email) is sent to payor, payee (client's assigned users), and the
  configured system-copy address.

### 5.4 Job Payment

**JobPayment**
- `Id`, `CreatedAtUtc`, `DatePaid` (nullable)
- `ClaimantUserId` (nullable) **or** `CollectionClientId` (nullable) — a job
  payment belongs to either an internal claimant or a collection client, not
  both (enforce via check constraint / validation).
- `Title`, `Description` (sent to payee), `InternalNote` (never sent/printed)
- `JobTotal`, `TotalDeductions`, `ClientProcessingFee`,
  `TotalTxnProcessingFee`, `TotalPaid` (computed: JobTotal − fees −
  deductions)
- Payout details: `PaidToName`, `PaidToAccountNo`, `PaidToBankName`,
  `PaidToBranch`, `PaymentTransactionNumber`
- `PaymentStatus`: `Processing` → `Submitted` → `Scheduled` → `Paid`. Only
  editable (line items add/remove) while `Processing`. `Scheduled` is set by
  Accountant and locks editing. `Paid` is set by Accountant with date +
  transaction number and triggers the payout email plus the cascading status
  updates in §5.4.1.
- Navigation: `Claims` (1+), `Collections` (all from the same
  `CollectionClientId` when present), `Deductions`, `Payrolls` (usually 1)

**JobPaymentDeduction**
- `Id`, `JobPaymentId`, `Description`, `Amount`

#### 5.4.1 Side effects on "Paid"

When a Job Payment transitions to `Paid`:
- All linked `Payroll`s → `Paid`
- All linked `Collection`s → `Transferred`
- All linked `Claim`s → `Honoured` (payment status)
- Payout notification email sent (client details, payment details/totals,
  itemized HTML tables for claims/collections/deductions each with their own
  subtotal)

### 5.5 Salary & Payroll

**SalaryDefinition**
- `Id`, `UserId`, `Description`, `BaseAmount`
- `FirstSalaryDate`, `LastSalaryDate`, `StartDate`, `EndDate`
- `RecurrenceDays`, `RecurrenceMonths`, `NearestDayInMonth` (day-of-week)
- `IsActive`
- `Adjustments` (1+ `SalaryAdjustment`)

**SalaryAdjustment**
- `Id`, `SalaryDefinitionId`, `Title`, `PercentageRate` (0.000–1.000),
  `FixedValue`, `Type` (`Deduction`, `Benefit`)

**Due-date algorithm** (§8.5 has full pseudocode):
`next = LastSalaryDate + RecurrenceMonths + RecurrenceDays`, then adjusted
to the nearest occurrence of `NearestDayInMonth`. Generate only if
`next >= StartDate` and `next <= EndDate` (or no `EndDate`). On generation,
`LastSalaryDate = next`.

**Payroll**
- `Id`, `UserId`, `PeriodEndingDate`, `Description`, `PayrollTotal`,
  `Status` (`Generated`, `Submitted`, `Paid`)
- `Entries` (ordered `PayrollEntry` list)
- Can only be created by the salary engine — no manual creation UI.

**PayrollEntry**
- `Id`, `PayrollId`, `Description`, `Amount` (signed), `IsLocked`,
  `SortOrder`
- Ordering contract: `Base` first, then `Benefit` entries (from salary
  adjustments), then `Deduction` entries (from salary adjustments), then
  custom entries last.
- Entries generated from the salary definition are `IsLocked = true` and not
  editable. Custom entries may be added while `Status = Generated`; a
  negative custom entry cannot exceed the remaining base after existing
  deductions; positive custom entries are unrestricted (beyond sane
  validation).
- On `Submitted`: entries become fully locked, and a `JobPayment` is
  auto-created for the user (claimant = the payroll's user) wrapping this
  payroll. `Payroll.Status` becomes `Paid` when that Job Payment is marked
  Paid (§5.4.1).

### 5.6 Audit & email log

**AuditLogEntry**
- `Id`, `OccurredAtUtc`, `ActorUserId`, `IsMcp` (bool), `Action`
  (e.g. `Claim.Accepted`, `JobPayment.MarkedPaid`), `EntityType`, `EntityId`,
  `DataBefore`/`DataAfter` (JSON, optional), `Notes`
- Visible to Administrator and Manager roles (Manager likely scoped to
  claims/collections/job-payment actions; Administrator sees everything —
  confirm scoping if it needs to be role-restricted per section).

**EmailLog** — see §3.1.

---

## 6. Feature areas by role

### 6.1 Claim & default dashboard (User)
- List own claims with status; "Add claim" button (creates `Draft`).
- Edit/delete only while not `Accepted`.
- Section showing payments made (Job Payments where this user is claimant,
  with status).

### 6.2 Teller dashboard
- Recent collections (last 24h) with review + reissue-receipt actions.
- New collection form as per §5.3, including client-scoped `Purpose`/`Amount`
  option lists.
- On save: persist, send receipt notifications (payor/payee/system copy),
  and offer an HTML "open printable receipt" view.

### 6.3 Manager dashboard
- **Claims review**: status filter (default `Submitted`), accept/reject,
  public comment, private comment, add to existing or new Job Payment
  (Accepted only).
- **Collections review**: filter by client, add to existing or new Job
  Payment (same client only, `Collected` status only).
- **Job Payments review**: list + detail, print / resend notification,
  remove line items while `Processing`, add deductions.

### 6.4 Accountant dashboard
- Everything in Manager, plus:
  - Accepted jobs list → "Mark scheduled"
  - Scheduled jobs list → "Mark paid" (date + transaction number, sends
    payout email)
  - View salary definitions, view payroll

### 6.5 Administrator
- User management (create/deactivate users by email, assign roles).
- Everything else in the system.

---

## 7. Notifications summary

| Event | Recipients | Content |
|---|---|---|
| Collection recorded | Payor, Payee (client's users), system-copy address | Printable HTML receipt |
| Job Payment marked Paid | Payee/claimant | Client/claimant details, payment totals & bank info, itemized claims/collections/deductions tables |

All notifications are also written to `EmailLog`.

---

## 8. Cross-cutting requirements

1. **Privacy page** — a real privacy policy page (not placeholder text),
   linked from the site footer.
2. **CDN-only front-end libs** — Bootstrap and jQuery must be referenced from
   a CDN (e.g. jsDelivr/cdnjs); do not add them to `wwwroot/lib`.
3. **Favicon** — a purpose-designed SVG icon, not a stock/default one.
4. **Remove scaffold cruft** — no leftover `Class1.cs` in either project.
5. **Logging** — every controller action, service method, and hosted service
   takes an `ILogger<T>` and logs meaningful entry/exit/error events.
6. **AGENTS.md** — repo root file with contribution rules for AI coding
   agents working in this repo, and instructions describing how a
   `MEMORY.md` file should be maintained/used across sessions.

---

## 9. Open items to confirm before/while implementing

These are places where the requirements are either implicit or could be
read more than one way — worth a quick confirmation rather than guessing
silently during build:

- Whether `AuditLogEntry` visibility for **Manager** should be scoped to
  claims/collections/job-payments only, or everything except user
  management.
- Whether a `User` can hold more than one role at once (spec describes a
  single-role hierarchy, but "Manager can access claims management and
  teller features" reads like Manager *inherits* Teller rather than a
  literal multi-role assignment — modeled here as a single-role hierarchy).
- Exact list of purpose-of-payment and amount presets are client-configured
  free lists — confirm whether Administrators or the client's own assigned
  users manage that list.
- Google SSO: any restriction to a Workspace domain, or any Google account
  allowed (as with Timetable, likely "any Google account" but worth
  confirming since this system handles money).