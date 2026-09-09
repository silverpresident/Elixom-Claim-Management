# Claude Specification Completeness Report

**Thorough reassessment:** 2026-09-09

**Source specification:** [`context/claude-specs.md`](../context/claude-specs.md)

**Method:** Inspected the current implementation, entities/mappings/migrations, MVC/API/MCP/worker boundaries, authorization paths, UI/print output, release documentation, and test results. This report describes the checked-out code, not planned Sprint 13 work.

## Executive conclusion

Sprint 12 is complete and the application is substantially implemented. Claims, clearing-house collections, job payments, payroll, Google SSO, OAuth/PKCE, durable outbox, standard MCP, scoped REST API/OpenAPI, audit trigger, and the HTML frontend all have implementation and automated evidence.

The project is **not release-ready** because Sprint 13 still has source-fidelity and release-control work. The additive literal audit/email persistence migration is now present locally, but compatibility callers and Bcc-only system-copy behaviour remain to be completed. Manager audit domain filtering and complete payout-notification fidelity also remain gaps. The data-preserving migration rehearsal is blocked on a designated production owner and production-shaped Azure SQL restore; independent OAuth/MCP review is also still required.

## Verification executed

```bash
dotnet test ElixomClaim.slnx --no-restore --logger "console;verbosity=minimal"
```

| Project | Passed | Failed |
| --- | ---: | ---: |
| `ElixomClaim.Lib.Tests` | 129 | 0 |
| `ElixomClaim.Web.Tests` | 107 | 0 |
| Total | 236 | 0 |

The full suite completed without dependency-vulnerability, redundant-package, or obsolete-Testcontainers warnings.

## Requirement assessment

| Requirement area | Status | Evidence / variation |
| --- | --- | --- |
| .NET 10 solution and Lib/Web/test split | Implemented | Required projects/layers exist; no `Class1.cs` remains. Domain/data/services are in Lib; MVC/OAuth/MCP/hosted adapters are in Web. |
| Azure SQL `dbclaim`, Guid IDs, JMD, UTC, display numbers | Implemented | Guid technical IDs, `decimal(18,2)`, JMD, UTC persistence, concurrency fields, and database sequence-backed record numbers are implemented. |
| Clean migrations and append-only audit trigger | Implemented for current baseline | The clean Guid baseline and audit-trigger migration apply on relational tests. The historical ledger was reset only after the documented confirmation that no deployed schema/data required preservation. |
| Google SSO/provisioned users/bootstrap admin | Implemented | Google-only sign-in, active provisioned-user validation, no password flow, and bootstrap admin seed/promotion are wired. |
| Single hierarchical role model | Implemented | Blocked/User/Teller/Manager/Accountant/Administrator hierarchy, policies, and shared service checks are present. |
| Claims workflow | Mostly implemented | Own draft create/edit/delete, submit, management decision, comments, soft deletion, payment state, payment history, MVC/API/MCP support, and tests exist. Comment append-only behaviour remains service-enforced rather than a database constraint. |
| Collections and receipts | Mostly implemented | Client configuration, bank details, options, assignments, server-owned fee snapshots, free entry snapshots, teller capture, 24-hour queue, reissue, durable receipt queue, and HTML print are present. |
| Job payment lifecycle/settlement | Mostly implemented | Exactly-one-payee invariant; Processing-only lines/deductions; compatible attachments; totals/fees; submit/schedule/paid cascade; immutable paid records; adjustment flow; and idempotent notification records are implemented. |
| Salary and payroll | Implemented | Shared recurrence planner, deterministic weekday tie-break, bounds, locked generated entries, custom-negative protection, submitted-job creation, and hosted scheduling are present. |
| Email transports/outbox | Mostly implemented | SMTP/ACS implementations, durable outbox, retry/backoff, valid-recipient continuity, skipped optional payor outcome, and per-attempt email logs exist. Header-field fidelity is not yet implemented. |
| Custom OAuth server | Mostly implemented | Dynamic public PKCE-only registration, confidential client validation, consent, strict redirects, code/token hashing, refresh family rotation/replay revocation, scopes, rate limits, and audits are present. Independent review remains. |
| Official MCP transport/tools | Implemented with residual assurance gap | Official stateless `/mcp` requires Bearer, `mcp:access`, and rate limit; six domain classes are registered. Real protocol tests cover discovery, invocation, scope denial, ownership, cancellation, and legacy-route retirement. Revoked/expired-token transport coverage is not explicit. |
| MCP/API email and preview boundaries | Implemented | Approved templates only; no arbitrary recipient/body; queues delegate to Lib services. Preview ownership/redaction is shared: Teller-owned collections, manager own-user payouts, and Accountant/Administrator reconciliation scope. |
| Durable operations | Implemented | Actor-owned idempotent reservations, status reads, approved salary run, and durable outbox wake-up lifecycle exist. The worker leases, completes/fails, and reclaims stale wake-ups; restart recovery is tested. |
| Versioned API/OpenAPI | Implemented | `/api/v1` has approved claims, collection, job-payment, template, payroll, and operation operations behind `api:access`; commands use idempotency. Authenticated `/openapi/v1.json` excludes MCP. |
| Privacy/CDN/favicon/HTML-only frontend | Implemented | Privacy page/footer, Jamaican contact, Bootstrap/jQuery CDN with SRI, SVG favicon, semantic views, and HTML print are present. |
| Audit record model and visibility | Partial | Trigger/redaction/audit events and additive `EntityType`/`EntityId`/`OccurredAtUtc` persistence migration now exist. Manager domain filtering and removal of compatibility use remain Sprint 13 work. |
| Email record header model | Partial | Records and adapters now have `To`/`From`/`Cc`/`Bcc` fields, but queue callers still use compatibility recipient paths and configured system copies are not yet Bcc-only. |
| Payout detail rendering | Partial | Print output now uses accessible responsive tables with category subtotals. The payout notification HTML still needs the equivalent table/subtotal and authorised-recipient/bank-detail fidelity. |
| `ILogger<T>` coverage | Mostly implemented | Most concrete controllers/services/workers/tools log structured outcomes. A few remaining classes lack it; see finding 5. |

## Material remaining gaps

### 1. Literal audit model is locally implemented but not fully integrated

The source specification defines `EntityType`, `EntityId`, and `OccurredAtUtc`. [`AuditRecord.cs`](../src/ElixomClaim.Lib/Entities/AuditRecord.cs) and migration `20260909124540_StructuredAuditAndEmailHeaders` now persist those fields and retain compatibility properties/source columns for the transition. The append-only trigger remains in place.

This remains incomplete until query projections stop using the legacy compatibility path, relational migration tests prove the backfill/trigger invariants, and the staging rehearsal succeeds. ADR 0008 and the [literal audit/email migration runbook](runbooks/literal-audit-email-migration.md) define the required additive, data-preserving approach.

### 2. Literal email headers exist, but Bcc system copies are not implemented

[`NotificationEntities.cs`](../src/ElixomClaim.Lib/Entities/NotificationEntities.cs) now persists `To`, `From`, `Cc`, and `Bcc`; sender and outbox contracts carry them. Compatibility `Recipient` properties and queue callers remain during the transition. System copies are still separate recipients/outbox rows, not Bcc-only headers.

Sprint 13 item 3 must update entities, mapping, sender adapters, queues/retries, preview/API/MCP projections, redaction, and test behaviour. Bcc data must never appear in bodies, previews, logs, unauthorized queries, or errors.

### 3. Manager audit visibility policy is decided but not enforced

Sprint 13's ready criteria choose Manager metadata-only access for claims, collections, and job payments; all other audit domains and before/after state remain Administrator-only. Current [`AdminController.cs`](../src/ElixomClaim.Web/Controllers/AdminController.cs) instead returns the latest 200 audit records without filtering action/target domain for a Manager.

This allows Manager metadata visibility for OAuth/security, user-administration, payroll, salary, email, and system events. Implement the accepted domain filter in shared queries/projections and add pagination/redaction/authorization tests.

### 4. Payout notification fidelity remains incomplete

[`Print.cshtml`](../src/ElixomClaim.Web/Views/JobPayments/Print.cshtml) now renders claims, collections, payrolls and ordered entries, deductions, and the calculation in captioned responsive tables with header scopes and category subtotals. `JobPaymentPrintViewTests` verifies those semantics as well as the absence of internal notes.

`JobPaymentService.ComposePayoutHtml` has improved notification content to include linked payrolls, but it still represents categories as lists rather than the source-specified itemized tables/subtotals. Remaining Sprint 13 item 4 work is to align notification data/layout, include authorised recipient/bank/totals detail, and prove internal notes/unauthorised bank data remain excluded.

### 5. Literal logging requirement has narrow remaining exceptions

`grep` found no `ILogger<T>` injection in:

- `ApprovedEmailPreviewService`
- `McpToolActorAccessor`
- `SalaryRecurrencePlanner`
- `SystemClock`

The planner and clock are deterministic utilities; Sprint 13 should either record a formal pure-utility exemption or add safe logging where a meaningful decision/outcome occurs. `ApprovedEmailPreviewService` and the actor accessor are security-sensitive boundaries and should have redacted structured outcome/security logs.

### 6. Release controls are externally blocked

The literal migration rehearsal is correctly blocked pending a named release owner and production-shaped Azure SQL restore. The runbook requires one runner, observer, safe email configuration, script checksum/idempotence, append-only validation, backup/PITR evidence, and redacted sign-off. This cannot be completed by local code changes alone.

The independent OAuth/MCP review runbook exists, but independent reviewer selection, review execution, remediation, and go/no-go evidence remain release gates. Explicit revoked/expired bearer transport coverage should also be added as part of that assurance work.

## Resolved since the prior report

- Sprint 12 is complete; standard MCP and the separately scoped API are implemented, documented, and tested.
- Payout email data now includes linked payrolls (though not the final required table/subtotal presentation).
- Payout print output now has accessible responsive tables, ordered payroll-entry rows, and category subtotals; focused `JobPaymentPrintViewTests` passed (3 tests) on 2026-09-09.
- README/MEMORY now accurately state active delivery and the Sprint 13 release-fidelity scope.
- Current full test evidence is 236 passing tests.

## Intentional or acceptable variations

| Source wording | Current implementation | Assessment |
| --- | --- | --- |
| IDs may be int or Guid | Guid technical IDs plus display sequences | Improvement and consistently applied. |
| Simple email queue | Durable transactional outbox with retries/idempotency | Reliability improvement. |
| No detailed paid-record correction flow | Approval-based linked adjustments/reversals | Protects financial history. |
| Example `/mcp/sse` | Official stateless Streamable HTTP `/mcp` | Valid standards-conformant equivalent. |
| System copy as CC/BCC | Separate recipient record | Delivery-equivalent only; Sprint 13 intentionally changes this to Bcc-only literal fidelity. |

## Recommended completion order

1. Obtain the release-owner/production-shaped restore needed to finish Sprint 13 item 1.
2. Complete and rehearse the additive audit/email model migration (items 2–3), including Bcc-only system copies and authorised projections.
3. Implement Manager audit domain filtering and finish payout-notification table/subtotal fidelity (items 4–5); the print portion is complete.
4. Resolve/document logging exceptions and complete revoked/expired-token assurance.
5. Complete independent OAuth/MCP review and release controls before production go/no-go.

## Delivery-state note

Sprint 12 is complete. Sprint 13 is active, with its literal-model migration/rehearsal blocked on external release-environment ownership. This report is an assessment artefact and does not claim any backlog item.
