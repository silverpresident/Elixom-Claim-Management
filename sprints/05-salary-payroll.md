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
| 2 | Complete | 2026-09-03 | Added shared `SalaryRecurrencePlanner` with a documented earlier-occurrence tie rule, effective-date checks, inactive suppression, and generated-period suppression. `SalaryRecurrencePlannerTests` verifies month boundaries, leap year, start/end inclusivity, inactive definitions, and duplicate prevention. Verified `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore --filter FullyQualifiedName~SalaryRecurrencePlannerTests` (7 passed). |
| 3 | Complete | 2026-09-03 | Added Accountant-authorized `SalaryPayrollService.GenerateForDefinitionAsync`, which uses `SalaryRecurrencePlanner`, creates locked Base/Benefit/Deduction entries in deterministic order, calculates exact JMD totals, advances `LastSalaryDate`, and audits the transaction. `SalaryPayrollServiceTests` covers generated entries, total, cursor, authority, and duplicate period. Verified `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore --filter FullyQualifiedName~SalaryPayrollServiceTests` (2 passed). |
| 4 | Complete | 2026-09-03 | Added Accountant-only custom-entry and submission operations to `SalaryPayrollService`. Generated entries stay locked; negative custom entries cannot make net pay negative; submission locks all entries and atomically creates one linked Processing `JobPayment`. `SalaryPayrollServiceTests` covers the restriction, locking, job creation, and immutable submitted state. Verified `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore --filter FullyQualifiedName~SalaryPayrollServiceTests` (3 passed). |
| 5a | Complete | 2026-09-03 | Registered the existing OAuth bearer handler and added Accountant-only `/mcp/payroll/preview` and `/mcp/payroll/run` adapters using explicit DTOs in `Mcp/Tools/PayrollTools.cs`. Both delegate to `ISalaryPayrollService` and write `IsMcpOperation` audit events. Verified `dotnet test src/ElixomClaim.Web.Tests/ElixomClaim.Web.Tests.csproj --no-restore` (27 passed). |
| 5 | Complete | 2026-09-03 | Added `SalaryGenerationHostedService` daily runner and Accountant-only `/payroll/salary-definitions/{id}/generate` action, both delegating to `ISalaryPayrollService`; database due-period uniqueness provides cross-instance idempotency. MCP prerequisite 5a adds audited Accountant preview/run adapters. Verified `dotnet test src/ElixomClaim.Lib.Tests/ElixomClaim.Lib.Tests.csproj --no-restore --filter FullyQualifiedName~SalaryPayrollServiceTests` (3 passed) and `dotnet build src/ElixomClaim.Web/ElixomClaim.Web.csproj --no-restore` (success). |
| 6 | Complete | 2026-09-03 | `/payroll` now provides Accountant definition creation, `ISalaryPayrollService` calculation previews, Generate Now/Submit lifecycle actions, validation messages, state clarity, and scoped audit history. Verified `dotnet build src/ElixomClaim.Web/ElixomClaim.Web.csproj --no-restore` (success). |
| 7 | Complete | 2026-09-03 | Focused recurrence (`SalaryRecurrencePlannerTests`), payroll calculation/locking/submission (`SalaryPayrollServiceTests`), and settlement cascade (`JobPaymentServiceTests`) cover the required lifecycle. Verified full Lib suite (90 passed) and Web suite (27 passed). |
