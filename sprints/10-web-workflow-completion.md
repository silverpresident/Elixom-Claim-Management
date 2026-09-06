# Sprint 10 — User, Job, and Payroll Workflow Completion

## Prerequisites

- Sprint 09 data and migrations must be complete before exposing its fields in MVC routes and views.
- Each command must remain a thin MVC adapter over a shared, authorization-aware Lib service and must record mutations through the audit service.

## Ordered backlog

1. Deliver the ordinary-user profile and bank-details management route/UI, plus a user dashboard payment-history section. Apply ownership checks, redacted display rules, validation, audit records, and responsive/accessible Razor forms.
2. Expose claim `DateOfJob`; collection-client description, internal notes, and fee configuration; collection-client-bank-detail internal notes; and payor telephone in the appropriate create/edit/detail MVC workflows. Keep internal-only fields out of receipt, print, and unauthorized projections.
3. Deliver manager job-payment workflows for creation, payee selection, claim/collection discovery and attachment/removal, deductions, title/description/internal note editing, review, and submission. Enforce Processing-only edits and client/payee compatibility through the shared service.
4. Deliver accountant job-payment workflows for scheduling and marking paid with validated UTC payment date and transaction metadata, plus the existing adjustment approval flow. Show bank snapshots and itemized totals only to authorized roles and retain paid-job immutability.
5. Deliver salary-definition adjustment management and accountant custom-payroll-entry MVC flows. Preserve generated-entry locking, custom-entry ordering, non-negative net-pay validation, payroll submission semantics, and audit history.
6. Replace the boilerplate home page with a role-aware useful landing/work-queue experience and ensure the navbar provides role-appropriate navigation to all implemented areas without disclosing unavailable or unauthorized routes.
7. Add endpoint and browser-oriented integration coverage for every new workflow, including ownership/role denials, sensitive-field redaction, lifecycle validation, accessibility validation summaries, and print/email exclusion of internal data.

## Done when

- Users, Managers, and Accountants can complete their specified work end to end in the MVC application without bypassing domain services.
- New fields are usable where authorized, protected where sensitive/internal, and all workflow state transitions have integration coverage.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-03 | Ordinary-user profile & bank details route/UI (`ProfileController`, `Views/Profile/Index.cshtml`) and claims dashboard payment history (`ClaimsController`, `Views/Claims/Index.cshtml`, `UserDashboardViewModel`). Verified with Playwright screenshot and 162 unit/integration tests passing (`ProfileControllerTests`, `ClaimsControllerTests`). |
| 2 | Complete | 2026-09-03 | Exposing claim DateOfJob, collection client description/notes/fees, bank detail notes, and payor telephone across MVC workflows and domain services. Verified with 165 solution unit and integration tests passing (`WorkflowFieldsCompletionTests`). |
| 3 | Complete | 2026-09-03 | Manager job-payment workflows for creation, payee selection (User vs Collection Client), claim/collection attachment & removal, deduction additions, metadata editing, and submission. Verified with 166 solution unit and integration tests passing (`ManagerJobPaymentsWorkflowTests`). |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
| 7 | Not started | — | — |
