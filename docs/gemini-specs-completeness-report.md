# Gemini Specification Completeness Report

**Reviewed:** 2026-09-08

**Source:** [`context/gemini-specs.md`](../context/gemini-specs.md)
**Method:** Static review of the solution, migrations, runtime composition, MVC/MCP routes, domain and security services, tests, and the current Sprint 12 ledger. This is an implementation assessment, not a production security certification.

## Executive conclusion

The core claims, collections, job-payment, payroll, identity, audit, notification, and browser UI workflows are substantially implemented. The solution has the required .NET 10 Lib/Web/test-project split, EF Core `dbclaim` model, Google allow-list sign-in, hierarchical roles, durable email outbox, HTML print views, and a standard authenticated MCP server.

It is **not fully complete** against `gemini-specs.md`. Material open differences are: comments are chronological rather than threaded; collection options are suggestions rather than mandatory choices; MCP operations are not durably recorded before execution and one can dispatch work directly; OAuth does not check requested scopes against each client's allowed scopes; and clean-SQL migration evidence remains unresolved. The Gemini `/mcp/sse` endpoint is deliberately superseded by the current SDK's Streamable HTTP `/mcp` endpoint.

## Requirement coverage

| Gemini requirement | Status | Evidence and assessment |
| --- | --- | --- |
| .NET 10/C# 14, MVC, EF Core, Azure SQL, `dbclaim` | Implemented | [`ElixomClaim.slnx`](../ElixomClaim.slnx), [`ApplicationDbContext.cs`](../src/ElixomClaim.Lib/Data/ApplicationDbContext.cs), and the project files use the requested platform and split. |
| Lib/Web/test project separation | Implemented, with variation | The four projects exist. MCP tools reside in Web rather than Lib, complying with this repository's transport-adapter boundary. |
| Startup migrations and bootstrap administrator | Implemented with deployment qualification | [`Program.cs`](../src/ElixomClaim.Web/Program.cs) invokes [`ApplyDatabaseMigrationsAsync`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs) outside development and seeds/promotes the configured administrator. Its lock is process-local; production requires a single migration runner. |
| Google SSO and active-user allow list | Implemented | Google wiring and [`UserValidationEvents.cs`](../src/ElixomClaim.Web/Authentication/UserValidationEvents.cs) authenticate only active provisioned users. |
| Role hierarchy/access matrix | Implemented | [`UserRoleExtensions.cs`](../src/ElixomClaim.Lib/Entities/UserRoleExtensions.cs), shared policies, and protected controllers implement User through Administrator access; inactive/Blocked users are denied. |
| Claims dashboard, lifecycle, own-draft editing/soft deletion, payment history | Mostly implemented | [`ClaimService.cs`](../src/ElixomClaim.Lib/Services/ClaimService.cs) and [`ClaimsController.cs`](../src/ElixomClaim.Web/Controllers/ClaimsController.cs) implement ownership/state rules, `DateOfJob`, payment states, UTC fields, row versions, and soft deletion. |
| Public/private threaded comments | Partially implemented | [`ClaimComment`](../src/ElixomClaim.Lib/Entities/ClaimEntities.cs) has public/private chronological comments but no parent-comment/thread relation. |
| Teller workspace, 24-hour collections, receipt reissue/print | Implemented | [`CollectionsController.cs`](../src/ElixomClaim.Web/Controllers/CollectionsController.cs) supplies collection, reissue, and print routes; [`HomeController.cs`](../src/ElixomClaim.Web/Controllers/HomeController.cs) calculates a 24-hour count. Print is `/collections/{id}/print`, not `/Teller/PrintReceipt/{id}`. |
| Client/payor/method/bank data and suggested purpose/amount | Implemented | [`CollectionEntities.cs`](../src/ElixomClaim.Lib/Entities/CollectionEntities.cs) provides clients, options, required bank branch/account type, payor data, methods, and timestamps. Teller-entered purpose/amount values are permitted as immutable transaction snapshots; matching active suggestions retain their links. |
| Receipt delivery and HTML-only output | Implemented | [`CollectionService.cs`](../src/ElixomClaim.Lib/Services/CollectionService.cs), [`OutboxService.cs`](../src/ElixomClaim.Lib/Services/OutboxService.cs), and [`Print.cshtml`](../src/ElixomClaim.Web/Views/Collections/Print.cshtml) queue/render HTML receipts. No PDF feature/dependency was found. |
| Manager claim/collection review and job assembly | Implemented | [`ManagerClaimsController.cs`](../src/ElixomClaim.Web/Controllers/ManagerClaimsController.cs), [`JobPaymentsController.cs`](../src/ElixomClaim.Web/Controllers/JobPaymentsController.cs), and [`JobPaymentService.cs`](../src/ElixomClaim.Lib/Services/JobPaymentService.cs) cover review, compatible attachment/removal, and deductions. |
| Job details, fees, schedule, and atomic payment | Implemented | Job entities and service include claims, collections, payrolls, deductions, notes, payout metadata, lifecycle locks, fee snapshots, settlement updates, and idempotent notification. Paid-record adjustments/reversals add a financial safeguard beyond Gemini. |
| Salary recurrence, adjustments, payroll generation/order/bounds | Implemented | [`SalaryPayrollService.cs`](../src/ElixomClaim.Lib/Services/SalaryPayrollService.cs) and [`SalaryRecurrencePlanner.cs`](../src/ElixomClaim.Lib/Services/SalaryRecurrencePlanner.cs) implement definitions, recurrence, generated locked entries, custom-entry non-negative validation, submission, and bound job creation. |
| Accountant scheduling/payment execution | Implemented | Accountant queue and settlement actions are in `JobPaymentsController`; payroll actions are accountant-only in [`PayrollController.cs`](../src/ElixomClaim.Web/Controllers/PayrollController.cs). |
| OAuth authorization code + PKCE S256 | Mostly implemented | [`OAuthController.cs`](../src/ElixomClaim.Web/Controllers/OAuthController.cs) exposes register/authorize/token/revoke. [`OAuthService.cs`](../src/ElixomClaim.Lib/Services/OAuthService.cs) validates redirects, requires S256, hashes codes/tokens, persists consent, rotates refresh tokens, and revokes tokens. It does not validate requested scopes against `AllowedScopes`; client-secret policy is optional without explicit public/confidential client types. |
| MCP concrete identity and audit | Implemented | [`BearerTokenAuthenticationHandler.cs`](../src/ElixomClaim.Web/Authentication/BearerTokenAuthenticationHandler.cs), [`ActorResolver.cs`](../src/ElixomClaim.Web/Services/ActorResolver.cs), and [`McpToolActorAccessor.cs`](../src/ElixomClaim.Web/Mcp/Tools/McpToolActorAccessor.cs) resolve/audit the concrete active user. |
| MCP endpoint | Implemented by protocol variation | `Program.cs` configures official `ModelContextProtocol.AspNetCore` stateless Streamable HTTP at `/mcp`, requiring Bearer authentication, `mcp:access`, and rate limiting. This replaces the older `/mcp/sse` example. |
| MCP groups and safe email/operations behavior | Partially implemented | Six classes and 14 attributed tools exist in [`Mcp/Tools`](../src/ElixomClaim.Web/Mcp/Tools); email tools are constrained to approved templates. Several read tools query `ApplicationDbContext` directly. `operations_outbox_wakeup` calls `DispatchDueAsync` directly, operation records are written after action execution, and `operations_status` does not filter by `ActorUserId`. |
| Audit logging | Implemented with naming variation | [`AuditService.cs`](../src/ElixomClaim.Lib/Services/AuditService.cs) stores `AuditRecords` rather than `AuditLogs`; migration `20260903090000_AddAuditRecordAppendOnlyTrigger` protects append-only persistence. |
| SMTP/ACS async notification queue and logs | Implemented by stronger variation | The durable database outbox/hosted dispatcher supersedes Gemini's in-memory `Channel<T>`. [`EmailSenders.cs`](../src/ElixomClaim.Lib/Services/EmailSenders.cs) supports SMTP/ACS; send outcomes are retained in `EmailLogs`. |
| CDN frontend, SVG favicon, print, privacy page | Implemented | [`_Layout.cshtml`](../src/ElixomClaim.Web/Views/Shared/_Layout.cshtml), [`favicon.svg`](../src/ElixomClaim.Web/wwwroot/favicon.svg), print styles, and [`Privacy.cshtml`](../src/ElixomClaim.Web/Views/Home/Privacy.cshtml) meet this requirement. |
| Guid identity and user-facing record numbers | Implemented | The sequence-number migration (`20260907103000_AddUserFacingSequenceNumbers`) preserves Guid technical keys while providing durable display values. |

## Differences and delivery risks

1. **Threaded comments are absent.** Add a parent-comment relationship and relevant ordering/authorization tests if threading remains required.
2. **Durable MCP operations are incomplete.** Operations execute before a durable reservation; direct outbox dispatch violates the request-only worker boundary; operation-status lookup must enforce actor ownership.
3. **OAuth policy needs hardening.** Enforce requested-scope subset checks and define/enforce public versus confidential client authentication.
4. **Migration release proof is incomplete.** The initial migration visibly retains earlier numeric identifiers while the live model uses Guid keys. Sprint 12 item 5b records clean-SQL/data-preserving reconciliation as planned. Empty and existing database migrations must be proven before release.
5. **Logging coverage is incomplete.** `AdminController`, `ClaimsController`, `HomeController`, `JobPaymentsController`, `ManagerClaimsController`, and MCP tools have no `ILogger<T>` dependency. Audit remains present but structured operational logging falls short of the stated standard.
6. **Documentation and dependency debt remain.** `README.md` still claims the implementation is unscaffolded. A high-severity transitive `SSH.NET` advisory (`GHSA-q939-rpr3-3284`) exists in Lib tests. The OAuth/security review in `MEMORY.md` remains open.
7. **Current repository scope remains unfinished.** `/api/v1` is absent despite being a Sprint 12 commitment; it is additional to the Gemini source specification.

## Intentional/beneficial variations

| Gemini specification | Implementation | Assessment |
| --- | --- | --- |
| `AuditLogs` | `AuditRecords` | Naming variation; append-only trigger is stronger than an ordinary mutable log table. |
| In-memory `Channel<T>` | Durable EF-backed outbox | Stronger restart safety, retries, and idempotency. |
| `/Teller/PrintReceipt/{id}` | `/collections/{id}/print` | Equivalent HTML print feature under a REST-style route. |
| `/mcp/sse` | Streamable HTTP `/mcp` | Current MCP protocol evolution, not a functional omission. |
| No correction path specified | Audited adjustment/reversal jobs | Additional financial-control safeguard. |
| Technical IDs only | Guid keys plus `SequenceNo` display labels | Improves usability without weakening relationships/audit identity. |

## Verification performed on 2026-09-08

```bash
dotnet test ElixomClaim.slnx --no-restore
dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore --filter 'FullyQualifiedName!~AuditRecordRelationalPersistenceTests'
```

- The solution invocation built Lib, Web, and both test assemblies. The Web suite passed: **71 passed, 0 failed**.
- The SQL Server/Testcontainers relational Lib test did not return a final result within this environment's 30-second command window. It requires a Docker/SQL-capable release environment, so this report does not claim a current full-suite outcome.
- Excluding that container-dependent test, Lib tests passed: **118 passed, 0 failed**.
- Observed warnings: the SSH.NET advisory, two `NU1510` unnecessary package-reference warnings, and an obsolete Testcontainers builder warning.

## Overall assessment

The repository is **functionally close to Gemini-spec complete**, with central business workflows implemented and broadly tested. It should not be represented as fully complete or production-ready until the threading decision, durable MCP operation boundary, OAuth scope/client enforcement, clean relational migration proof, logging/dependency debt, and remaining Sprint 12 release evidence are closed.
