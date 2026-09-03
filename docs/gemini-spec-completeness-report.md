# Gemini Specification Completeness Report

**Reviewed:** 2026-09-03  
**Source specification:** [`context/gemini-specs.md`](../context/gemini-specs.md)  
**Scope:** Read-only comparison of the current repository implementation, tests, sprint ledger, and runtime wiring. This report does not alter application behavior.

## Executive conclusion

The core domain services and data model are substantially implemented, but the application is **not complete against the Gemini specification**. The strongest coverage is in claims, collection recording, payment settlement, payroll generation, durable email delivery, and basic OAuth token lifecycle behavior.

Material gaps remain in MCP transport, audit immutability, production migration startup, several user-facing workflows, OAuth hardening, and the in-progress Development testing experience. The repository also has an unbuildable Web test project at the reviewed revision.

## Implemented requirements

| Requirement area | Assessment | Principal implementation evidence |
| --- | --- | --- |
| .NET 10 MVC solution, Lib/Web/test split, `dbclaim` schema, JMD `decimal(18,2)` values | Implemented | [`ApplicationDbContext.cs`](../src/ElixomClaim.Lib/Data/ApplicationDbContext.cs), [`DependencyInjection.cs`](../src/ElixomClaim.Lib/DependencyInjection.cs) |
| Google-based allow-listed active-user access and hierarchical roles | Implemented | [`UserValidationEvents.cs`](../src/ElixomClaim.Web/Authentication/UserValidationEvents.cs), [`UserRoleExtensions.cs`](../src/ElixomClaim.Lib/Entities/UserRoleExtensions.cs) |
| Claim lifecycle, ownership enforcement, soft delete, public/private comments | Mostly implemented | [`ClaimService.cs`](../src/ElixomClaim.Lib/Services/ClaimService.cs), [`ClaimEntities.cs`](../src/ElixomClaim.Lib/Entities/ClaimEntities.cs) |
| Collection clients, client-scoped options, collection recording, HTML receipts and reissue | Mostly implemented | [`CollectionService.cs`](../src/ElixomClaim.Lib/Services/CollectionService.cs), [`CollectionsController.cs`](../src/ElixomClaim.Web/Controllers/CollectionsController.cs) |
| Durable SMTP/ACS/fake email delivery, retry and email logs | Implemented | [`OutboxService.cs`](../src/ElixomClaim.Lib/Services/OutboxService.cs), [`EmailSenders.cs`](../src/ElixomClaim.Lib/Services/EmailSenders.cs) |
| Job payment source constraints, total calculations, scheduling/settlement service logic, paid-state cascade | Mostly implemented | [`JobPaymentService.cs`](../src/ElixomClaim.Lib/Services/JobPaymentService.cs) |
| Salary recurrence, generated payrolls, custom-entry bounds and payroll-to-job submission | Mostly implemented | [`SalaryPayrollService.cs`](../src/ElixomClaim.Lib/Services/SalaryPayrollService.cs), [`SalaryRecurrencePlanner.cs`](../src/ElixomClaim.Lib/Services/SalaryRecurrencePlanner.cs) |
| OAuth endpoints, PKCE S256, token hashing, refresh rotation/revocation, bearer user projection | Partially implemented | [`OAuthController.cs`](../src/ElixomClaim.Web/Controllers/OAuthController.cs), [`OAuthService.cs`](../src/ElixomClaim.Lib/Services/OAuthService.cs), [`BearerTokenAuthenticationHandler.cs`](../src/ElixomClaim.Web/Authentication/BearerTokenAuthenticationHandler.cs) |
| Domain-scoped MCP-style adapters, constrained template email and operation commands | Partially implemented | [`Mcp/Tools`](../src/ElixomClaim.Web/Mcp/Tools), [`McpClaimsController.cs`](../src/ElixomClaim.Web/Controllers/McpClaimsController.cs) |
| Bootstrap/jQuery CDN usage, SVG favicon, privacy page, HTML-only printing | Implemented | [`_Layout.cshtml`](../src/ElixomClaim.Web/Views/Shared/_Layout.cshtml), [`Privacy.cshtml`](../src/ElixomClaim.Web/Views/Home/Privacy.cshtml) |

## Material differences and missing requirements

### 1. MCP transport is not implemented as specified

The Gemini specification requires an MCP transport endpoint such as `/mcp/sse`. The repository has custom authenticated REST endpoints under `/mcp/claims`, `/mcp/collections`, `/mcp/email`, `/mcp/job-payments`, `/mcp/operations`, and `/mcp/payroll`, but no SSE endpoint or standard MCP server transport.

**Impact:** MCP clients expecting the specified transport cannot connect. The current endpoints provide MCP-adjacent capabilities rather than a verified MCP protocol implementation.

### 2. Audit records are not database-append-only

Audit service redaction and persistence exist, but `AuditRecords` has no database-level prevention of `UPDATE` or `DELETE`. The model and initial migration contain no trigger, restricted database permission boundary, or equivalent immutable-write mechanism.

This is also recorded as blocked in [`sprints/01-identity-security.md`](../sprints/01-identity-security.md).

**Impact:** The audit accountability invariant can be bypassed by a database actor, so the audit requirement is incomplete.

### 3. Production migration startup is not wired

The specification calls for migrations to be applied on startup. The repository contains [`DatabaseMigrationExtensions.cs`](../src/ElixomClaim.Lib/Data/DatabaseMigrationExtensions.cs), but [`Program.cs`](../src/ElixomClaim.Web/Program.cs) does not invoke the migration extension.

**Impact:** A deployed application does not automatically apply pending migrations as specified.

### 4. Claim data and claimant experience are incomplete

The following Gemini requirements are absent:

- `DateOfJob` is not present on `Claim` or the claim create/edit form.
- Comments are append-only but not threaded; `ClaimComment` has no parent/comment-thread relationship.
- There is no profile or bank-details management route/UI for ordinary users.
- The claim dashboard lists claims and their payment state, but has no separate payment-history section.

The implemented claim state transitions, ownership checks, soft delete, and management comments are otherwise present.

### 5. Collection capture omits payor telephone

The Gemini collection workflow requires optional payor telephone. `CollectionTransaction`, `RecordCollectionCommand`, and the collection form provide payor name and email but no telephone field.

**Impact:** The record cannot preserve all required collection contact data.

### 6. Job-payment service coverage exceeds its UI coverage

The shared service supports creation, attachments, deductions, submission, scheduling, settlement, and adjustment operations. The MVC controller/views expose list/detail, collection review/attachment, print, accountant queue, and payout resend, but do not provide user-facing creation, deduction, submission, scheduling, mark-paid, or adjustment approval workflows.

The model also differs from the specification: it has `PublicNote` and `InternalNote`, but no job title or separately modelled public description.

**Impact:** Important payment mechanics are callable in code but not fully operable through the specified Manager and Accountant dashboards.

### 7. Salary and payroll administration is incomplete

- Salary adjustments are represented and used during generation, but no UI or service command exists to manage them after definition creation.
- The salary definition create form does not expose the entity's `EndDate` field.
- Custom payroll entries are supported by `SalaryPayrollService`, but no MVC endpoint/view allows an accountant to add them.

**Impact:** The implemented payroll engine cannot be fully configured or operated through the intended web experience.

### 8. OAuth implementation needs additional hardening

The OAuth service covers several high-value controls, including PKCE S256, exact redirect matching in the authorization GET path, token hashing, rotation, replay-family revocation, and bearer identity projection. It does not yet meet all stated requirements:

- Access and refresh lifetimes are hard-coded to one hour and 14 days instead of using the configured `OAuthOptions` values.
- No rate limiting or throttle mechanism is implemented.
- Consent is displayed but not persisted as a consent record.
- Dynamic registration has no redirect-URI shape validation or explicit registration admission policy.
- `OAuthAuthorizationCode.Code` stores the raw authorization code in addition to its hash.
- The authorization consent POST does not revalidate the client and redirect URI before redirecting. Form-field tampering therefore needs remediation to prevent an open-redirect path.

**Impact:** This remains a high-risk protocol surface and should not be treated as production-ready without the missing controls, interoperability verification, and independent review.

### 9. Development-testing work is incomplete and currently breaks the Web test build

Sprint 07 is explicitly `In progress`. Development-only in-memory seeding and role-selectable sign-in work are present, but the Web test file has syntax errors at lines 77 and 79 of [`AccountControllerTests.cs`](../src/ElixomClaim.Web.Tests/Controllers/AccountControllerTests.cs).

**Impact:** The full solution test suite cannot compile or pass at the reviewed revision, and the sprint's required verification is not complete.

## Intentional or acceptable variations from Gemini

| Gemini specification | Current implementation | Assessment |
| --- | --- | --- |
| `AuditLogs` name | `AuditRecords` | Naming variation; the important missing part is immutability, not the table name. |
| In-memory `Channel<T>` email queue | Durable database outbox plus hosted dispatcher | Improvement: supports retries and idempotency across process restarts. |
| MCP content in the shared library | MCP adapters in Web, domain decisions in Lib | Aligns with the repository's later layering rule. |
| `/Teller/PrintReceipt/{id}` | `/collections/{id}/print` | Equivalent feature under a different route. |
| No paid-record correction process defined | Linked accounting adjustment/reversal flow | Additional protection and auditability beyond the Gemini baseline. |

## Delivery governance observations

The sprint ledger is inconsistent with the repository's ordered-delivery protocol:

- Sprint 01 remains `In progress`, with OAuth and several security items marked `Not started`.
- Sprint 06 claims MCP/OAuth readiness work is complete.
- Later sprints are marked complete despite Sprint 01 still being active.

This does not necessarily mean the code is absent, but it makes completion evidence and remaining security work difficult to rely on.

## Verification performed

Command run:

```bash
dotnet test ElixomClaim.slnx --no-restore
```

Outcome:

- `ElixomClaim.Lib.Tests`: **92 passed**.
- `ElixomClaim.Web.Tests`: did not compile because of the two syntax errors described above.
- Build emitted two non-blocking `NU1510` package-pruning warnings in `ElixomClaim.Lib.csproj`.

## Overall assessment

The repository is a strong service-layer foundation with many critical lifecycle rules covered by tests. It should, however, be considered **partially implemented rather than complete** against `gemini-specs.md` until the protocol/security gaps and missing end-user workflows are resolved and the full test suite is restored.
