# Claude Specification Completeness Report

**Reviewed:** 2026-09-03
**Requested source:** `clause-specs.md`
**Source actually present:** [`context/claude-specs.md`](../context/claude-specs.md)
**Scope:** Static review of the current implementation, migrations, tests, delivery ledger, and runtime registration. This report does not change application code.

## Executive assessment

The repository is **substantially implemented but not complete** against the available Claude specification. Its strongest, test-backed areas are the layered .NET solution, role hierarchy, claims lifecycle, collection recording and HTML receipts, job-payment settlement rules, salary/payroll generation, durable email dispatch, and the core OAuth code/refresh-token flow.

The application should not yet be considered production-complete. The most material gaps are audit-record immutability, a standard MCP transport, incomplete user-facing workflows for jobs and payroll, incomplete data fields required by the specification, and the lack of several expected OAuth and migration-production controls. Sprint 07 development-testing work is also still in progress.

## Status key

| Status | Meaning |
| --- | --- |
| Implemented | Code and tests support the requirement at the reviewed revision. |
| Mostly implemented | The core behavior exists, with a material omission or limited UI/coverage. |
| Partial | Some supporting code exists but the stated outcome is not available end-to-end. |
| Missing | No conforming implementation was found. |
| Variation | Deliberate or neutral implementation difference; assess whether it is acceptable. |

## What is implemented

| Claude spec area | Status | Evidence and assessment |
| --- | --- | --- |
| §1–3 solution split, .NET 10 MVC, Lib/Web/test projects, EF Core schema `dbclaim` | Implemented | [`ApplicationDbContext.cs`](../src/ElixomClaim.Lib/Data/ApplicationDbContext.cs) sets the default schema; Lib/Web and both xUnit projects exist. |
| SQL Server configuration, `decimal(18,2)` money and JMD handling | Implemented | EF precision mappings cover monetary domain fields; service/model tests cover core totals. |
| Google-only provisioned-user sign-in, bootstrap administrator, hierarchical single role | Implemented | [`Program.cs`](../src/ElixomClaim.Web/Program.cs), [`UserValidationEvents.cs`](../src/ElixomClaim.Web/Authentication/UserValidationEvents.cs), and authorization tests implement cookie/Google sign-in, active-user validation, bootstrap behavior, and policies. |
| Claims creation, ownership, submission, accept/reject, comments, soft deletion and hidden-by-default reads | Mostly implemented | [`ClaimService.cs`](../src/ElixomClaim.Lib/Services/ClaimService.cs) and [`ClaimServiceTests.cs`](../src/ElixomClaim.Lib.Tests/Services/ClaimServiceTests.cs) support the lifecycle; MVC claimant and manager pages are present. Missing claim fields are listed below. |
| Collections clients, assigned users, client-scoped purpose/amount options, collection state, receipt workflow | Mostly implemented | Entity relationships and composite option/client foreign keys are in [`ApplicationDbContext.cs`](../src/ElixomClaim.Lib/Data/ApplicationDbContext.cs); recording/reissue behavior is in [`CollectionService.cs`](../src/ElixomClaim.Lib/Services/CollectionService.cs). |
| HTML-only receipt delivery and printable routes | Implemented | [`CollectionsController.cs`](../src/ElixomClaim.Web/Controllers/CollectionsController.cs), receipt Razor view, durable outbox, sender implementations, and collection tests support this. No PDF generation was found. |
| SMTP, ACS, and durable email dispatch/retry | Implemented | [`EmailSenders.cs`](../src/ElixomClaim.Lib/Services/EmailSenders.cs), [`OutboxService.cs`](../src/ElixomClaim.Lib/Services/OutboxService.cs), and [`OutboxServiceTests.cs`](../src/ElixomClaim.Lib.Tests/Services/OutboxServiceTests.cs). |
| Job payment one-payee constraint, Processing-only changes, calculation, submission/scheduling/settlement and paid-state cascade | Mostly implemented | Database check constraint and [`JobPaymentService.cs`](../src/ElixomClaim.Lib/Services/JobPaymentService.cs) enforce the domain flow; tests include lifecycle, concurrency, and settlement coverage. Several manager/accountant UI actions are absent. |
| Salary recurrence, adjustments, generated locked entries, net-pay guard, submit-to-job flow | Mostly implemented | [`SalaryRecurrencePlanner.cs`](../src/ElixomClaim.Lib/Services/SalaryRecurrencePlanner.cs), [`SalaryPayrollService.cs`](../src/ElixomClaim.Lib/Services/SalaryPayrollService.cs), and their tests support the core engine and deterministic weekday tie-break. |
| Thin hosted schedulers for salary generation and outbox dispatch | Implemented | [`SalaryGenerationHostedService.cs`](../src/ElixomClaim.Web/HostedServices/SalaryGenerationHostedService.cs) and [`OutboxDispatchHostedService.cs`](../src/ElixomClaim.Web/HostedServices/OutboxDispatchHostedService.cs) delegate to Lib services. |
| Custom OAuth authorization server, code exchange, refresh/revocation, bearer user projection | Mostly implemented | [`OAuthController.cs`](../src/ElixomClaim.Web/Controllers/OAuthController.cs), [`OAuthService.cs`](../src/ElixomClaim.Lib/Services/OAuthService.cs), and bearer-handler tests demonstrate the core flow. Hardening differences appear below. |
| Domain-separated MCP-style API adapters and MCP audit marking | Partial | The six required tool class files exist under [`Mcp/Tools`](../src/ElixomClaim.Web/Mcp/Tools), with bearer-authenticated REST controllers and `IsMcp` audit writes. They are not a standard MCP transport. |
| Privacy page, footer link, CDN Bootstrap/jQuery, SVG favicon, no `Class1.cs` | Implemented | [`_Layout.cshtml`](../src/ElixomClaim.Web/Views/Shared/_Layout.cshtml), [`Privacy.cshtml`](../src/ElixomClaim.Web/Views/Home/Privacy.cshtml), and [`wwwroot/favicon.svg`](../src/ElixomClaim.Web/wwwroot/favicon.svg). |
| Root `AGENTS.md` and durable project memory | Implemented | [`AGENTS.md`](../AGENTS.md) and [`MEMORY.md`](../MEMORY.md) exist and contain operating guidance/current state. |

## Differences, omissions, and variations by specification clause

### §2–3: structure, infrastructure, email, and hosting

| Requirement | Status | Difference / impact |
| --- | --- | --- |
| Use one identifier convention consistently | Partial | The model uses `Guid` for users/clients and `long` for most financial records. The specification explicitly asks for one convention. This is a design variation, not a current functional defect. |
| Every entity has `CreatedAtUtc` | Partial | Several entities omit it, including `SalaryAdjustment` and the job-payment join entities. Most primary aggregates do carry creation timestamps. |
| `IEmailSender` with SMTP and ACS selected by `Email:Provider` | Mostly implemented | The abstraction and both implementations exist, but configuration is `Notifications:Provider`, not `Email:Provider`. This is a naming variation that must be reflected in deployment documentation. |
| Email log persists To/From/Cc/Bcc/subject/body/**SentAtUtc** | Partial | [`EmailLog`](../src/ElixomClaim.Lib/Entities/NotificationEntities.cs) stores one `Recipient`, subject/body/provider/status/attempt/creation time. It does not model From, Cc, Bcc, or a distinct sent timestamp. System copies are separate outbox recipients, which is functionally reasonable but not the literal requested audit shape. |
| System copy is CC/BCC on notifications | Variation | A configured system-copy address is queued as a separate message rather than a CC/BCC field. Delivery intent is satisfied, but message headers and log representation differ. |
| Automatic guarded production migration application | Missing at runtime | [`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) exists, but [`Program.cs`](../src/ElixomClaim.Web/Program.cs) does not call it. A deployed app therefore does not apply pending migrations at startup. |

### §4: authentication, authorization, OAuth, and MCP

| Requirement | Status | Difference / impact |
| --- | --- | --- |
| Allow-listed active Google user; no local password; bootstrap administrator | Implemented | Meets the stated access model. |
| Role policies and hierarchy | Implemented | Policy-driven authorization and inheriting role comparisons are present. |
| Standard MCP server secured by custom OAuth | Missing / Partial | No `/mcp/sse` (or other standard MCP protocol transport) is mapped. The present `/mcp/...` endpoints are bespoke bearer-authenticated REST adapters, so normal MCP clients cannot use them as a server transport. |
| MCP executes as the actual user and logs `IsMcp` | Mostly implemented | Token identity is projected to the request principal and tool methods audit MCP actions. Adapter-level authorization is not consistently delegated to the same shared business services; some tools query `ApplicationDbContext` directly. |
| MCP operations request durable, audited, idempotent background operations | Partial | [`OperationsTools.cs`](../src/ElixomClaim.Web/Mcp/Tools/OperationsTools.cs) uses a process-local static `ConcurrentDictionary` and directly calls salary/outbox services. It loses operation history on restart and does not provide the durable operation record required by the project guardrails. |
| OAuth authorization-code and refresh-token flow | Mostly implemented | Core registration, authorization, token, refresh rotation, revocation, hash lookup, and bearer authentication exist. |
| OAuth production hardening | Partial | The implementation needs an explicit production security review. Specific observable issues: access/refresh lifetimes are hard-coded rather than read from `OAuthOptions`; registration lacks redirect-URI admission/shape validation; authorization code entities retain raw `Code` as well as its hash; consent is displayed but not stored; no rate limiting was found; and the consent POST should revalidate its client/redirect inputs before redirecting. |

### §5.1–5.2: user profile and claims

| Requirement | Status | Difference / impact |
| --- | --- | --- |
| User bank name, account name, account number, and branch | Partial | [`User`](../src/ElixomClaim.Lib/Entities/User.cs) only holds account number and branch code. Account name and bank name are absent. |
| User profile and personal bank-details management | Missing | No normal-user profile/bank controller or view was found. |
| Claim `DateOfJob` and `DeletedAtUtc` | Missing | [`Claim`](../src/ElixomClaim.Lib/Entities/ClaimEntities.cs) has neither field; create/edit commands and views cannot collect them. |
| Claim workflow/payment status, comments, soft delete, claimant edit restrictions | Mostly implemented | Workflow/payment state and global soft-delete filtering exist. The implementation permits claimant edits/deletes in `Draft` and `Submitted`, matching the Claude specification; the current README narrows the prose differently in places, so this should be retained as an intentional spec choice. |
| Claim comments append-only and private-to-management behavior | Mostly implemented | Add/read behavior exists; comments also have `IsDeleted`, so database/model design technically permits soft deletion rather than making append-only immutable at the data layer. |

### §5.3: payment clearing house

| Requirement | Status | Difference / impact |
| --- | --- | --- |
| Collection client notes, per-job fee, and per-transaction fee | Partial | [`CollectionClient`](../src/ElixomClaim.Lib/Entities/CollectionEntities.cs) contains name/active state, assignments, options, and bank details but no Notes, `PerJobProcessingFee`, or `PerTransactionFee`. Fees are supplied in commands/calculated at job level instead of being client configuration. |
| Collection payor phone | Missing | Neither entity nor collection command/form contains optional payor phone. |
| Client-scoped purpose and amount options | Implemented | Options, uniqueness, active state, and client compatibility are enforced. |
| Collected → Processing → Transferred and attachment only when collected | Implemented | Domain service and status checks enforce this. |
| Receipt to payor, client recipients, and system copy | Mostly implemented | Recipients are queued from payor email, active assigned-client users, and system copy. The client is represented by assigned application users rather than a separate client email field. |

### §5.4: job payments and settlement

| Requirement | Status | Difference / impact |
| --- | --- | --- |
| Exactly one claimant/payee user or collection client | Implemented | Database check constraint `CK_JobPayments_ExactlyOnePayee` and service validation enforce it. |
| Job `Title`, public Description, InternalNote, and bank snapshot fields | Partial | Current entity has `PublicNote` and `InternalNote`, but no job title or distinct public description. It also lacks the requested payout bank-name/account-name/account-number/branch snapshot fields. |
| Processing-only line/deduction changes; schedule locks; paid is immutable | Mostly implemented | Shared service enforces lifecycle restrictions and adds a linked adjustment/reversal model. |
| Manager job UI: create, attach claims/collections, remove lines, deductions, submit | Partial | MVC exposes listing/detail, collection attachment, print, and resend, but no user-facing create, claim-attachment discovery, remove-line, deductions, or submit routes/forms were found. |
| Accountant UI: schedule and mark paid with date/transaction number | Partial | An accountant queue exists, but no MVC action/form exposes schedule or mark-paid. The shared service supports the transition. |
| Paid side effects: payroll Paid, collections Transferred, claims Honoured, notification | Implemented | Settlement service tests cover state cascade and idempotent notification behavior. |
| Notification includes detailed payout/bank data and itemized claims/collections/deductions subtotals | Partial | The system has an HTML payout composition path, but the model cannot supply all requested bank snapshots and the print view is minimal (claims only; no collection/deduction subtotals). |

### §5.5 and §6.4: salaries, payroll, and dashboards

| Requirement | Status | Difference / impact |
| --- | --- | --- |
| Salary fields, recurrence algorithm, inclusive bounds, deterministic nearest weekday, unique due period | Implemented | Entity/model constraints and recurrence planner satisfy the engine requirements. |
| Salary adjustments and generated payroll entries | Mostly implemented | Adjustments exist and generate locked ordered entries. No MVC/service command was found to add/manage adjustments through the intended accountant administration experience. |
| Custom payroll entries while generated, negative-net guard, locking and submit-to-job | Mostly implemented | Service behavior exists and is tested; no MVC action/view permits accountants to add custom entries. |
| User dashboard includes payment history | Missing | Claim pages show payment state but no separate claimant payment-history view/section was found. |
| Teller recent-24-hour queue and reissue | Implemented | Collections index, detail, receipt, and reissue actions exist. |
| Manager claims review and comments | Implemented | Queue/filter, accept/reject, and public/private comment actions exist. |
| Administrator user management | Implemented | User list/edit administrator routes and views are present. |

### §5.6 and §8: audit, privacy, frontend, and quality

| Requirement | Status | Difference / impact |
| --- | --- | --- |
| Audit persistence with actor/action/target/time/MCP and safe before/after state | Mostly implemented | Audit service and entity exist; sensitive-value redaction is implemented. |
| Audit records append-only | Missing | There is no database trigger, permission boundary, or equivalent UPDATE/DELETE prevention in the migration. This is explicitly recorded as blocked in [`sprints/01-identity-security.md`](../sprints/01-identity-security.md). |
| Manager-scoped audit visibility | Variation | Managers can access the audit log route. The source specification calls its exact scope an open item; the implementation does not visibly narrow results to claims/collections/jobs. |
| Bootstrap/jQuery CDN only; real privacy policy; SVG favicon; no scaffold cruft | Implemented | The inspected assets/views meet these requirements. |
| `ILogger<T>` in every controller, service, and hosted service | Partial | Most substantive controllers/services/hosted services inject logging. Several MCP controllers/tool classes and simple controllers do not visibly do so, so the literal “every” requirement is not met. |

## Intentional or potentially acceptable variations

| Specification wording | Current implementation | Assessment |
| --- | --- | --- |
| In-memory channel/email queue is implied by a simple background-job design | Durable database outbox with retries and idempotency | Improvement: stronger delivery semantics for financial notifications. |
| No paid-record correction process defined | Linked auditable adjustment/reversal job payments | Improvement: preserves paid financial history. See [`ADR 0002`](../adr/0002-reversal-adjustment-accounting.md). |
| Suggested tool set is limited | Six domain tool groups plus constrained email/operation adapters | Broader surface is acceptable only after standard MCP transport and security controls are complete. |
| Example `/Teller/PrintReceipt/{id}` route | `/collections/{id}/print` | Equivalent user-facing capability under a cleaner route. |
| `AuditLogEntry` name | `AuditRecord` | Neutral naming variation; immutability is the real issue. |
| Client system copy as CC/BCC | Separate durable recipient records | Operationally useful but differs in headers and email-log schema. |

## Delivery and governance observations

- The working tree contains unrelated, uncommitted Sprint 07 development-testing changes. Its progress row remains `In progress`; this audit did not alter those files.
- [`MEMORY.md`](../MEMORY.md) marks Sprint 01 as `In progress` because audit append-only enforcement is blocked, while later domain sprints are marked complete. This accurately calls out the remaining risk but means release readiness must not be inferred from the later sprint statuses alone.
- The persisted `appsettings.json` contains development placeholder configuration and a LocalDB connection string. Deployment must continue to supply real secrets through user secrets, Key Vault, or deployment configuration as documented.

## Verification performed

Command run from the repository root:

```bash
dotnet test ElixomClaim.slnx --no-restore
```

Result: **passed** — 92 `ElixomClaim.Lib.Tests` tests and 38 `ElixomClaim.Web.Tests` tests (130 total). The build emitted two non-blocking `NU1510` warnings that `Microsoft.Extensions.Options.ConfigurationExtensions` and `Microsoft.Extensions.Options.DataAnnotations` may be unnecessary package references in `ElixomClaim.Lib`.

## Recommended completion order

1. Enforce audit-record append-only behavior at the database level and add relational integration coverage.
2. Implement a standard MCP transport, replace in-memory operation tracking with durable audited operations, and complete OAuth hardening/threat-model actions.
3. Add the missing required data fields/migrations: claim job/delete dates, payor phone, user bank name/account name, collection-client notes/fees, and payout bank snapshots.
4. Complete manager/accountant web workflows for job creation/lifecycle/deductions and payroll adjustments/custom entries.
5. Wire the guarded migration runner for the intended deployment mode, complete Sprint 07, and add end-to-end authorization/UI tests for the missing workflows.
