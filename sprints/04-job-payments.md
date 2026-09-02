# Sprint 04 — Job Payments and Settlement

## Ordered backlog

1. Add Job Payment, claim/collection/payroll associations, deductions, payout fields, JMD calculated-total rules that preserve exact two-decimal values without additional rounding, status enum, source/payee exclusivity constraint, immutability constraints, and concurrency controls.
2. Implement shared commands to create/manage Processing jobs, attach accepted claims, attach Collected collections of one client, remove items, and manage deductions. Recalculate totals server-side.
3. Build Manager job-payment lists/detail, filtered collection review, attachment flows, internal-note-safe print/detail view, and resend-notification command.
4. Implement submit and Accountant schedule commands; scheduling locks edits and is audited.
5. Implement the atomic mark-paid command: require payment date/transaction number, update all linked states, create one payout outbox record, and prevent replay with idempotency/concurrency protection.
6. Compose responsive payout HTML with totals, safe bank/payment information, and itemized/subtotalled claims, collections, deductions. Never expose internal notes.
7. Add Accountant accepted/scheduled queues and integration tests for every invalid/valid state transition, totals, cross-client rejection, and cascade.
8. Implement the linked reversal/adjustment payment workflow for Paid records, including authorization, immutable original linkage, accounting rules, audit events, and notifications; test that Paid records cannot be edited/deleted.

## Done when

- Only accountants can settle an immutable scheduled payment; all linked records and the notification outcome remain consistent after retries/failures.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-02 | Added `JobPayment`, claim/collection/payroll association entities, deductions, payout fields, exact `decimal(18,2)` JMD totals, lifecycle enum, row-version concurrency, and SQL check/unique constraints. Migration: `20260902221255_AddJobPaymentEntities`. Tests: `JobPaymentModelTests`; `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore` passed (74). |
| 1a | Complete | 2026-09-02 | Added minimal `Payroll` persistence record and constrained one-payment association required by item 1. It deliberately omits salary definition, entry, and generation behavior, which remains Sprint 05 scope. |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
| 7 | Not started | — | — |
| 8 | Not started | — | — |
