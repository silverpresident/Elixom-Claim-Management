# Gemini Specification Completeness Report

**Reviewed:** 2026-09-10
**Source:** [`context/gemini-specs.md`](../context/gemini-specs.md)
**Assessment scope:** Repository implementation, migrations, tests, configuration, and delivery ledger. This is not an independent security assessment or a production-release approval.

## Conclusion

The four Gemini business workflows are substantially implemented: claims, payment collections, job payments, and salary/payroll. The repository also implements the required .NET 10 MVC/EF Core architecture, Google allow-list sign-in, hierarchical role checks, built-in OAuth authorization-code flow with PKCE S256, authenticated `/mcp`, durable HTML email delivery, audit persistence, printable HTML views, and the specified CDN frontend assets.

It is **not accurate to describe the implementation as completely verified or production-ready** today. Sprint 13 is active, with literal audit/email migration work incomplete and release controls outstanding. More immediately, the current API integration suite has one failure after the Bcc system-copy change, and the collection receipt reissue path still writes obsolete recipient-only email records rather than the current header model. The full test command therefore is not green.

Status terms used below:

| Status | Meaning |
| --- | --- |
| Implemented | A corresponding implementation path and evidence were found. |
| Implemented with variation | The required result exists, but the implementation deliberately differs from the Gemini wording. |
| Partial / verification gap | Material code, test, migration, or operational evidence remains incomplete. |

## Requirement-by-requirement assessment

| Gemini area | Status | What is implemented / evidence | Difference, limitation, or remaining evidence |
| --- | --- | --- |
| .NET 10, C# 14, MVC, EF Core, Azure SQL, `dbclaim` | Implemented | The four projects are in [`src`](../src); [`ApplicationDbContext`](../src/ElixomClaim.Lib/Data/ApplicationDbContext.cs) defaults EF objects to `dbclaim`, configures Azure SQL-compatible mappings, `decimal(18,2)` money, and Guid relationships. | Azure SQL production deployment/rehearsal is not evidenced locally. Sprint 13 item 1 is blocked on a production-shaped Azure SQL restore rehearsal. |
| Project/layer structure | Implemented with variation | Business entities, persistence, and services are in `ElixomClaim.Lib`; MVC, Razor, authentication, hosted services, REST, and MCP adapters are in `ElixomClaim.Web`. | Gemini's illustrative tree places MCP and queue concerns in Lib and names folders differently. The actual layout keeps HTTP/MCP adapters in [`Mcp/Tools`](../src/ElixomClaim.Web/Mcp/Tools), consistent with the repository architecture. |
| Startup migrations and bootstrap administrator | Implemented with operational qualification | [`Program.cs`](../src/ElixomClaim.Web/Program.cs) calls `ApplyDatabaseMigrationsAsync`; [`DatabaseMigrationExtensions`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) applies relational migrations and seeds/promotes `DefaultAdminEmail`. Production SQL Server uses a session-scoped `sp_getapplock`. | The implementation requires a designated/single production migration runner. `20260909124540_StructuredAuditAndEmailHeaders` is pending rehearsal. |
| Google SSO and active-user allow list | Implemented | Google/cookie authentication is configured in [`Program.cs`](../src/ElixomClaim.Web/Program.cs); [`UserValidationEvents`](../src/ElixomClaim.Web/Authentication/UserValidationEvents.cs) validates the active provisioned user. [`AdminController`](../src/ElixomClaim.Web/Controllers/AdminController.cs) administers users. | No Google-domain restriction: any Google account may enter only when it matches an active provisioned record. |
| Blocked through Administrator role matrix | Implemented | [`UserRoleExtensions`](../src/ElixomClaim.Lib/Entities/UserRoleExtensions.cs), shared policies, service checks, and protected routes implement hierarchy; Blocked never satisfies a minimum role. | Manager audit access is deliberately narrower than Gemini's broad Manager/Administrator wording: only selected operational metadata is intended for Managers. Sprint 13 item 5 remains in progress. |
| User dashboard and claim management | Implemented | [`ClaimService`](../src/ElixomClaim.Lib/Services/ClaimService.cs), [`ClaimsController`](../src/ElixomClaim.Web/Controllers/ClaimsController.cs), dashboard/view models, and claim views cover drafts, submission, ownership, edit/delete, history, and status presentation. [`ApplicationDbContext`](../src/ElixomClaim.Lib/Data/ApplicationDbContext.cs) globally hides soft-deleted claims. | The current lifecycle restricts claimant mutation to the appropriate pre-acceptance/draft state. |
| Claim content, comments, and privacy | Implemented | `Claim` has title, description, job date, amount, timestamps, workflow/payment status, soft-delete and concurrency fields. `ClaimComment` supports append-only public and management-private comments. | No Gemini omission found. |
| Teller workspace and 24-hour collections | Implemented | [`CollectionsController`](../src/ElixomClaim.Web/Controllers/CollectionsController.cs) supports entry, details, reissue, and print. [`HomeController`](../src/ElixomClaim.Web/Controllers/HomeController.cs) projects teller collection activity. [`CollectionService`](../src/ElixomClaim.Lib/Services/CollectionService.cs) authorizes tellers and persists records. | The HTML receipt route is `/collections/{id}/print`, rather than Gemini's illustrative `/Teller/PrintReceipt/{id}`. |
| Collection client, payor, suggestions, methods, and bank details | Implemented | [`CollectionEntities`](../src/ElixomClaim.Lib/Entities/CollectionEntities.cs) and [`CollectionClientAdministrationService`](../src/ElixomClaim.Lib/Services/CollectionClientAdministrationService.cs) model client options, payor contacts, Cash/Pos/BankTransfer/CreditNote, immutable transaction snapshots, and required bank details/account types. `RecordAsync` validates client-scoped active suggestions and retains matching links. | The project adds processing-fee snapshots and client-user controls. |
| Collection receipt delivery and HTML-only print | Partial / verification gap | Creation queues durable `EmailOutboxItem` records; [`OutboxService`](../src/ElixomClaim.Lib/Services/OutboxService.cs) dispatches them, and [`Print.cshtml`](../src/ElixomClaim.Web/Views/Collections/Print.cshtml) is the print surface. No PDF generator/package was found. | Normal recipients now use `To`/`From`/`Bcc`, but `ReissueReceiptAsync` writes obsolete non-persisted `Recipient` only, leaving persisted headers blank. The no-payor skipped-record branch does the same. This is an incomplete Sprint 13 header migration path. |
| Manager review and job assembly | Implemented | [`ManagerClaimsController`](../src/ElixomClaim.Web/Controllers/ManagerClaimsController.cs), [`ManagerCollectionsController`](../src/ElixomClaim.Web/Controllers/ManagerCollectionsController.cs), [`JobPaymentsController`](../src/ElixomClaim.Web/Controllers/JobPaymentsController.cs), and [`JobPaymentService`](../src/ElixomClaim.Lib/Services/JobPaymentService.cs) cover review, comments, filtering, compatible attachment/removal, and deductions. | Shared-service authorization and one-client collection-job invariants are enforced. |
| Job-payment fees, locks, and settlement | Implemented | Jobs include claims, collections, payrolls, deductions, payee, notes, payout metadata, lifecycle, fee snapshots, and `TotalPaid`. `JobPaymentService` permits changes only in Processing and atomically updates related claims, collections, payrolls, and payout notifications. | The repository adds auditable reversal/adjustment jobs ([ADR 0002](../adr/0002-reversal-adjustment-accounting.md)), a beneficial control beyond Gemini. |
| Salary recurrence and payroll | Implemented | [`SalaryPayrollService`](../src/ElixomClaim.Lib/Services/SalaryPayrollService.cs), [`SalaryRecurrencePlanner`](../src/ElixomClaim.Lib/Services/SalaryRecurrencePlanner.cs), and [`SalaryGenerationHostedService`](../src/ElixomClaim.Web/HostedServices/SalaryGenerationHostedService.cs) implement bounds, recurrence, adjustments, ordered/locked generated entries, non-negative custom net pay, bound jobs, and paid propagation. | The current specification documents a deterministic nearest-weekday tie-break; Gemini only says “nearest.” |
| Accountant scheduling/payment execution | Implemented | Accountant queue/detail/schedule/mark-paid actions are wired through `JobPaymentsController` and `JobPaymentService`; payment date and transaction number are required at settlement. | External provider/staging notification evidence remains Sprint 13 release work. |
| OAuth authorization server and PKCE | Implemented | [`OAuthController`](../src/ElixomClaim.Web/Controllers/OAuthController.cs) exposes `/oauth/register`, `/oauth/authorize`, `/oauth/token`, and `/oauth/revoke`. [`OAuthService`](../src/ElixomClaim.Lib/Services/OAuthService.cs) validates redirects/scopes, requires S256, hashes credentials, rotates refresh tokens, records consent, and revokes tokens. | Registration and revocation are beneficial additions. Independent OAuth/MCP review remains a release blocker. |
| MCP identity, roles, audit, and endpoint | Implemented with variation | [`BearerTokenAuthenticationHandler`](../src/ElixomClaim.Web/Authentication/BearerTokenAuthenticationHandler.cs) resolves bearer tokens to active users. [`Program.cs`](../src/ElixomClaim.Web/Program.cs) mounts official stateless Streamable HTTP at `/mcp`, authenticated, scoped, and rate-limited. Six tool groups exist. | Gemini refers to custom middleware/SSE. The implementation uses an authentication handler and official MCP transport, while retaining the required concrete-user identity boundary. |
| MCP safe email/operations behavior | Implemented | [`EmailTools`](../src/ElixomClaim.Web/Mcp/Tools/EmailTools.cs) only previews/queues approved templates. [`OperationsTools`](../src/ElixomClaim.Web/Mcp/Tools/OperationsTools.cs) uses idempotent operation records and does not invoke worker internals. | Sprint 13 item 6 still needs token-lifecycle, cancellation, and full API-contract evidence. |
| Audit persistence and mutation logging | Partial / verification gap | [`AuditRecord`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs), [`AuditService`](../src/ElixomClaim.Lib/Services/AuditService.cs), mappings, and append-only trigger migrations persist actor/action/entity/state/network data. The literal model uses `EntityType`, `EntityId`, and `OccurredAtUtc`. | Obsolete legacy audit calls remain in `ApprovedOperationService`, `ApprovedEmailPreviewService`, and `CollectionClientAdministrationService`. Sprint 13 item 2 remains in progress. |
| SMTP/ACS async notification and logs | Implemented with stronger variation, but partial migration | Durable outbox dispatch is hosted by [`OutboxDispatchHostedService`](../src/ElixomClaim.Web/HostedServices/OutboxDispatchHostedService.cs); [`EmailSenders`](../src/ElixomClaim.Lib/Services/EmailSenders.cs) supports SMTP and ACS; `EmailLogs` retains outcomes. | Gemini calls for an in-memory Channel worker. The durable database outbox is stronger for retries/restarts/idempotency. Bcc header migration/retry/redaction evidence remains incomplete. |
| HTML-only responsive receipt/email | Implemented | Receipt and payout composition are HTML; printable Razor views include print presentation. No PDF feature was found. | Sprint 13 item 4 is complete for itemized payout print; notification parity/redaction needs continued coverage. |
| Bootstrap/jQuery CDN, SVG favicon, privacy page | Implemented | [`_Layout.cshtml`](../src/ElixomClaim.Web/Views/Shared/_Layout.cshtml), [`favicon.svg`](../src/ElixomClaim.Web/wwwroot/favicon.svg), [`site.css`](../src/ElixomClaim.Web/wwwroot/css/site.css), and [`Privacy.cshtml`](../src/ElixomClaim.Web/Views/Home/Privacy.cshtml) provide the requested assets. | `favicon.ico` also exists; it does not conflict with the SVG requirement. |
| Guid identity plus user-facing record numbers | Implemented | Database sequences and `SequenceNo` mappings exist for claims, comments, collection clients/transactions, jobs, salaries, payrolls, and line records. Views/message subjects use sequence labels where available. | No Gemini omission found. |

## Material differences and variations

| Gemini wording | Actual implementation | Assessment |
| --- | --- | --- |
| In-memory `Channel<T>` and `EmailProcessingWorker` | EF-backed durable outbox and hosted dispatcher | Beneficial: restart safety, retries, idempotency, and delivery history. |
| `/Teller/PrintReceipt/{id}` | `/collections/{id}/print` | Equivalent HTML print feature under a different route. |
| Custom MCP middleware/SSE | Bearer handler plus official Streamable HTTP transport | Protocol/implementation variation; concrete identity is retained. |
| `EntityName`/`TimestampUtc` audits | `EntityType`/`EntityId`/`OccurredAtUtc`, with obsolete compatibility members | Deliberate model refinement; migration/caller work remains. |
| Visible system-copy recipient | System copy is Bcc-only | Privacy strengthening. The old API test has not caught up. |
| No correction path | Audited reversal/adjustment jobs | Beneficial financial-control addition. |

## Open gaps and release risks

1. **Receipt reissue headers:** Finish `CollectionService.ReissueReceiptAsync` and the skipped-recipient branch using persistent `To`, configured `From`, and Bcc system-copy fields; add dispatch/persistence tests.
2. **Failing API test:** `EmailTemplatesApi_QueuesOnlyApprovedRecipientsIdempotently` expects two visible outbox rows. Current Bcc semantics create one visible-recipient row with the system address in `Bcc`; update the test (or explicitly change the recipient model).
3. **Audit conversion:** Replace remaining obsolete string-target `IAuditService` calls and finish Manager/API/MCP projection coverage.
4. **Migration/release evidence:** The production-shaped Azure SQL rehearsal, designated migration owner, safe email test account, independent OAuth/MCP review, staging smoke tests, backup/restore, retention checks, and go/no-go record remain open Sprint 13 work.

## Verification performed

On 2026-09-10:

```bash
dotnet test ElixomClaim.slnx --no-restore --no-build
```

- `ElixomClaim.Web.Tests`: **108 passed, 1 failed**.
- Failed test: `ApiEndpointIntegrationTests.EmailTemplatesApi_QueuesOnlyApprovedRecipientsIdempotently` at line 358; expected two outbox records, actual one.
- The Lib test host did not complete within the tool window during concurrent reruns and was stopped to avoid orphaned test processes. It is therefore **not** reported as passing.
- A preceding solution test/build emitted obsolete-API warnings for `Recipient` and legacy audit calls, corroborating the incomplete migration findings.

## Overall assessment

Gemini's functional scope is largely present, including all four requested workflows and their main security, transport, and UI foundations. The accurate delivery state is **functionally substantial, but not fully verified or release-ready**. Finish the Sprint 13 audit/email-header migration paths and reconcile the API test before claiming full Gemini-spec completeness.
