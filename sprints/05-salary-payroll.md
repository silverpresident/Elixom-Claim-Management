# Sprint 05 — Salary and Payroll

## Ordered backlog

1. Add salary definition/adjustment, payroll, and ordered payroll-entry schema with constraints for adjustment ranges, immutable generated entries, and one payroll per salary definition/due period.
2. Implement and unit-test the due-date calculator, including month boundaries, leap years, nearest-weekday tie-break, start/end inclusivity, inactive definitions, and duplicate prevention.
3. Implement payroll generation in one transaction: base, benefits, deductions, locks, deterministic ordering, total, and `LastSalaryDate` update.
4. Add controlled custom entries while Generated; enforce that negative adjustments never make the payroll net negative. Submit locks entries and creates a linked Processing job payment.
5. Add daily hosted scheduler with distributed-safe/idempotent execution and manual authorized Generate Now action; keep both as calls to the same service.
6. Build Accountant salary/payroll screens with calculation previews, status clarity, validation, and audit history.
7. Test recurrence, amount calculations, locking, submission, job creation, and paid cascade from Sprint 04.

## Done when

- A salary definition creates exactly one correct payroll per eligible period, and its submitted payroll follows the normal job-payment settlement path.
