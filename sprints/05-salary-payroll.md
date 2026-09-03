# Sprint 05 — Salary and Payroll

## Ordered backlog

1. Add salary definition/adjustment, payroll, and ordered payroll-entry schema with constraints for adjustment ranges, immutable generated entries, and one payroll per salary definition/due period.
2. Implement and unit-test the due-date calculator, including month boundaries, leap years, nearest-weekday tie-break, start/end inclusivity, inactive definitions, and duplicate prevention.
3. Implement payroll generation in one transaction: base, benefits, deductions, locks, deterministic ordering, total, and `LastSalaryDate` update.
4. Add controlled custom entries while Generated; enforce that negative adjustments never make the payroll net negative. Submit locks entries and creates a linked Processing job payment.
5. Add daily hosted scheduler with distributed-safe/idempotent execution, manual authorized Generate Now action, and an Accountant-scoped MCP trigger that supports preview/run and delegates to the same service.
6. Build Accountant salary/payroll screens with calculation previews, status clarity, validation, and audit history.
7. Test recurrence, amount calculations, locking, submission, job creation, and paid cascade from Sprint 04.

## Done when

- A salary definition creates exactly one correct payroll per eligible period, and its submitted payroll follows the normal job-payment settlement path.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-03 | Rebased first-implementation EF history into `20260903053340_InitialCreate`, which includes mandatory salary definitions, adjustment ranges, payroll due-period identity, and ordered payroll entries. Updated `JobPaymentService` total field and migration-aware tests. Verified `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore` (80 passed) and `dotnet test src/ElixomClaim.Web.Tests/ElixomClaim.Web.Tests.csproj --no-restore` (27 passed). |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
| 7 | Not started | — | — |
