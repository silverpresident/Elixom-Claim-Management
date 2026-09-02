No, while the last response provided key technical implementation details (such as the OAuth 2.0 authorization server, MCP identity inheritance, and ACS/SMTP email pipeline), it **did not contain the complete specifications** for the entire business domain. It focused primarily on architectural infrastructure.

To give you a single, complete reference, here is the **Full Business & Technical Specification** for the Elixom Claim solution, bringing together all four core domain features, dashboards, data models, and infrastructure requirements.

---

# Elixom Claim — Complete Technical Specification

**Target Platform:** .NET 10.0 (C# 14) / ASP.NET Core MVC

**Database Schema:** `dbclaim` (Azure SQL Database, Auto-Applied EF Core Migrations on Startup)

**Authentication & Security:** Google SSO (Web App) + Built-in OAuth 2.0 Authorization Server with PKCE for MCP

**MCP Security Model:** Per-User Identity Context (`HttpContext.User` inheritance) with full Audit Trail logging

**Notifications:** Dual Delivery via Standard SMTP (`SmtpClient`) & Azure Communication Services (`EmailClient`), HTML receipts only (**NO PDF**)

**Frontend Stack:** CDN-hosted Bootstrap 5.3 & jQuery 3.7, Custom SVG Favicon

---

## 1. Solution Architecture & Project Structure

The codebase is organized within a root `src/` directory containing two main projects and two unit/integration test projects:

```text
/ (Repository Root)
├── AGENTS.md                   # AI Agent guidance, operational limits, and rules
├── MEMORY.md                   # Persistent context tracking and decision record
└── src/
    ├── ElixomClaim.Lib/        # Shared core library (Entities, Services, DbContext, MCP, Queue)
    │   ├── Context/            # EF Core DbContext, schema configuration ('dbclaim'), migrations
    │   ├── Entities/           # Domain models, OAuth models, Audit entities
    │   ├── Extensions/         # Startup auto-migrations & default admin seeder
    │   ├── Interfaces/         # Service abstractions (IEmailQueue, IAuditService, ISalaryService)
    │   ├── Models/             # DTOs, ViewModels, MCP tool payloads, OAuth request specs
    │   └── Services/           # Business logic, Salary Engine, Audit Service, Email Worker
    ├── ElixomClaim.Web/        # ASP.NET Core MVC Presentation Layer
    │   ├── Controllers/        # Claims, Teller, Manager, Accountant, Admin, OAuth, Auth Controllers
    │   ├── Middleware/         # Custom OAuth2 middleware & MCP SSE Endpoint Handler (/mcp/sse)
    │   ├── Views/              # Razor MVC views, printable HTML receipt views
    │   └── wwwroot/            # Scalable SVG favicon (favicon.svg), custom CSS
    ├── ElixomClaim.Lib.Tests/  # xUnit tests for core logic, salary recurrence, MCP security
    └── ElixomClaim.Web.Tests/  # xUnit integration tests for Controllers, OAuth, and Audit

```

---

## 2. User Roles & Identity Access Matrix

Authentication for human users is strictly handled via **Google OAuth 2.0 SSO**. User access is authorized via a whitelist database table (`dbclaim.Users`). New accounts are added via the Admin interface or bootstrap configuration; unauthorized email logins are rejected.

| Role | Core Functional Permissions |
| --- | --- |
| **Blocked** | Default role for locked or pending accounts. All access is forbidden (`403 Forbidden`). |
| **User** | Default active user. Create, view, edit, and soft-delete own draft claims; manage profile & bank details. |
| **Teller** | Access the **Payment Clearing House**. Collect payee payments, view 24h collections, print/reissue HTML receipts. |
| **Manager** | Inherits Teller capabilities. Review/accept/reject claims, review collections, build & manage Job Payments. |
| **Accountant** | Inherits Manager capabilities. Manage Salary definitions, execute Payroll runs, schedule & execute Job Payments. |
| **Administrator** | Access User Management (add/edit users, assign roles, block users), view system Audit Logs, full access. |

### 2.1 Configuration Bootstrapping (`appsettings.json`)

Allows defining a default administrator email that is automatically provisioned during startup:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    },
    "DefaultAdminEmail": "admin@elixom.com"
  },
  "Notifications": {
    "Provider": "ACS",
    "FromAddress": "no-reply@elixom.com",
    "ACSConnectionString": "endpoint=https://elixom.communication.azure.com/;accesskey=..."
  }
}

```

---

## 3. Four Core System Functions & Workflows

### 3.1 Function 1: Claim Management & User Dashboard

* **Default Dashboard:** Displays a list of claims submitted by the logged-in user with their current status (`Draft`, `Submitted`, `Accepted`, `Rejected`) and payment status (`Unprocessed`, `Processing`, `Honoured`), alongside payment history.
* **Claim Lifecycle & Rules:**
* Contains Title, Description, Date of Job, Total Claimed, Created Date, and Status.
* Can be edited or deleted by the claimant **only if not yet accepted**.
* Deletions execute as **soft deletes** (`IsDeleted = true`).
* Supports threaded comments (`dbclaim.ClaimComments`), differentiating between public comments and internal management private comments.



### 3.2 Function 2: Teller Dashboard (Payment Clearing House)

* **Access Scope:** Users with `Teller` role or higher.
* **Teller Workspace:** Shows recent collection transactions (last 24 hours), allows reviewing transactions, and reissuing printable HTML receipts.
* **Collection Entry Workflow:**
1. Select a Payee from a configured list of `CollectionClient` entities.
2. Input Payor Name, optional Email, and optional Telephone.
3. Select **Purpose of Payment** and **Amount Collected** (using dropdown choices pre-configured per `CollectionClient`).
4. Select **Method of Collection** (`Cash`, `POS`, `BankTransfer`, `CreditNote`).
5. Payment Date defaults to current timestamp.


* **Confirmation Trigger:**
* Saves transaction record to `dbclaim.CollectionTransactions`.
* Queues email notification receipt to Payor.
* Queues email notification receipt to Payee (`CollectionClient`).
* Queues email copy to system configured address.
* Generates an inline printable HTML receipt view (`/Teller/PrintReceipt/{id}`).



### 3.3 Function 3: Manager Dashboard

* **Claim Review:**
* View all claims filtered by status (defaults to `Submitted` / unpaid).
* Accept or Reject claims.
* Add public comments (visible to claimant) or private internal notes.
* Attach accepted claims to an existing Job Payment or create a new Job Payment.
* Attached claims update payment status to `Processing`.


* **Collection Review:**
* View collections filtered by `CollectionClient`.
* Attach collections to an existing or new Job Payment (must be in `Collected` status; all collections in a single Job Payment must belong to the same client).
* Collection status transitions: `Collected` $\rightarrow$ `Processing` (when assigned to job) $\rightarrow$ `Transferred` (when job is paid).


* **Job Payment Management:**
* **Composition:** Contains Date Created, Date Paid, List of Claims, List of Collections, List of Deductions, List of Payrolls, Target Claimant/Payee, Title, Public Description, Internal Note, and status (`Processing`, `Submitted`, `Scheduled`, `Paid`).
* **Deductions:** Managers/Accountants can add custom deduction line items (Description, Amount).
* **Fee Calculations:** Sums Client Processing Fee, Internal Processing Fees per transaction, Total Deductions, and calculates `TotalPaid = JobTotal - Fees - Deductions`.
* **Editable Rules:** Modifications and item removals are permitted only while status is `Processing`.



### 3.4 Function 4: Accountant Dashboard, Salary Engine & Payroll

#### Salary Engine (`dbclaim.SalaryDefinitions`)

* **Recurring Model:** Contains Base Amount, Start Date, End Date, First/Last Salary Date, Recurrence Days, Recurrence Months, Nearest Day in Month (DayOfWeek), User ID, and Adjustments (Benefits/Deductions with percentage or fixed values).
* **Due Date Calculation Algorithm:**

$$\text{Target Date} = \text{LastSalaryDate} + \text{RecurrenceMonths} + \text{RecurrenceDays}$$



*Adjusted to the nearest specified `NearestDayInMonth` day of the week.*
* **Validation:** Generates a salary item on or after the due date, provided the current date is between `StartDate` and `EndDate`.

#### Payroll Engine (`dbclaim.PayrollRecords`)

* **Generation:** Generated strictly from `SalaryDefinition` records (manual standalone creation forbidden).
* **Ordering Rule:** `PayrollEntries` are strictly ordered: **Base Salary** (Order 0), followed by **Benefits** (+), then **Deductions** (-), then **Custom Entries**.
* **Custom Entry Bounds:** Custom entries added to generated payrolls cannot reduce the net total below `$0.00`.
* **Submission Workflow:** Submitting a payroll updates its status to `Submitted` and automatically generates a bound `JobPayment` in `Processing` status. Updating the Job Payment to `Paid` updates the payroll to `Paid`.

#### Accountant Payment Execution

* View accepted Job Payments with bank details required for transfer.
* Transition jobs to `Scheduled` (locks the job from further edits).
* Mark scheduled jobs as `Paid` by supplying **Payment Date** and **Transaction Number**.
* Marking a job as `Paid` triggers the background email queue to dispatch official payment summary emails to all parties and transitions attached collections to `Transferred`, claims to `Honoured`, and payroll to `Paid`.

---

## 4. Built-in OAuth 2.0 Authorization Server & MCP Architecture

For AI Agent interactions via Model Context Protocol (MCP):

* **Endpoint Paths:** `/oauth/authorize`, `/oauth/token`, and `/mcp/sse`.
* **OAuth Protocol:** Full OAuth 2.0 Authorization Code grant with mandatory PKCE (`S256` code challenge).
* **Identity Inheritance:** MCP clients pass OAuth2 bearer tokens. The `McpAuthenticationMiddleware` resolves the token to the underlying `dbclaim.Users` account, injecting their exact identity and Role Claims into `HttpContext.User`.
* **Audit Enforcement:** Actions performed over MCP invoke `IAuditService` and are recorded in `dbclaim.AuditLogs` under the specific user's email and ID.

---

## 5. Audit Logging & System Email Engine

### 5.1 Audit Logs (`dbclaim.AuditLogs`)

All security events, role modifications, status transitions, collection entries, and payment state changes are written to the database with `UserId`, `UserEmail`, `Action`, `EntityName`, `EntityId`, `OldValuesJson`, `NewValuesJson`, `IpAddress`, and `TimestampUtc`. Accessible by `Manager` and `Administrator` roles.

### 5.2 Notification Pipeline (`IEmailQueue` + `EmailProcessingWorker`)

* **Asynchronous Queue:** Requests push `EmailMessageDto` onto an in-memory `Channel<T>`.
* **Background Dispatching:** An `IHostedService` worker consumes items and routes them to either Azure Communication Services or standard SMTP based on `Notifications:Provider`.
* **Audit Trail:** Every sent or failed email is logged to `dbclaim.EmailLogs`.
* **Document Constraint:** Generation or outputting of PDF files is strictly forbidden. Notifications and receipts use responsive Razor HTML templates, rendered in-browser for printing via native CSS media print styles.

---

## 6. Repository Operational Rules (`AGENTS.md`)

```markdown
# AI Agent & Engineering Operational Guidelines

## Operational Rules
1. **Target Stack:** .NET 10.0 (C# 14), ASP.NET Core MVC, Entity Framework Core.
2. **Database Schema:** Custom schema `dbclaim` on Azure SQL. Migrations and default admin seeding execute automatically on startup via `ApplyDatabaseMigrationsAsync()`.
3. **Authentication Strategy:**
   - Web Users: Google SSO OAuth2 verified against pre-provisioned whitelist (`dbclaim.Users`).
   - MCP AI Clients: Built-in OAuth2 Authorization Server (`/oauth/*`) using PKCE (`S256`).
4. **MCP Security:** MCP executions authenticate via OAuth2 tokens, inheriting caller identity, permissions, and role claims (`User`, `Teller`, `Manager`, `Accountant`, `Administrator`).
5. **No PDF Documents:** Do NOT introduce PDF generation libraries. Printable views must be rendered as clean HTML/CSS views (`/Teller/PrintReceipt/{id}`).
6. **Notification Queue:** Use background worker (`EmailProcessingWorker`) supporting Azure Communication Services and Standard SMTP.
7. **Audit Logging:** Log all mutations, OAuth2 token issuances, and MCP tool executions to `dbclaim.AuditLogs` via `IAuditService`.
8. **CDN Front-End:** Do not serve Bootstrap or jQuery locally in `wwwroot`. Use defined CDN links exclusively.
9. **Code Cleanliness:** Permanently remove `Class1.cs` upon initialization.

```

---

## 7. Persistent Context Memory (`MEMORY.md`)

```markdown
# Repository Architecture & Context Ledger

## Technical Baseline
- **Target Framework:** .NET 10 (C# 14) MVC
- **Database Schema:** `dbclaim`
- **Identity & OAuth:** Custom built-in OAuth 2.0 Authorization Server (`/oauth/*`) for MCP clients; Google SSO for web browser sessions.
- **MCP Security:** MCP tool executions inherit the caller's identity and log actions to `dbclaim.AuditLogs`.
- **Email Delivery:** Asynchronous Channel worker (`EmailProcessingWorker`) supporting ACS Email and Standard SMTP logging to `dbclaim.EmailLogs`.
- **Receipt Rendering:** Responsive Razor HTML templates; PDF output is explicitly forbidden.

## Key Architectural Commitments
1. **Central Shared Library (`ElixomClaim.Lib`):** Contains DbContext, Entities, Services, Salary Engine, Audit Engine, and MCP Tools.
2. **Soft Delete Standard:** Claims apply soft deletes (`IsDeleted = true`).
3. **Audit Accountability:** Every action executed manually or via MCP maps to the specific user account and writes to `dbclaim.AuditLogs`.

```