# Sprint 07 — Development Testing Experience

## Ordered backlog

1. Add an explicitly Development-only EF Core in-memory database, seeded with representative, non-sensitive data for identity, claims, collections, job payments, payroll, notifications, audit, and OAuth client configuration. Provide a Development-only role-selectable sign-in bypass for the active hierarchy roles; a Blocked seeded account must remain unable to authenticate.

## Done when

- Running with the Development environment uses the in-memory database and presents usable sample data across every implemented business area.
- The development sign-in route and UI are unavailable outside Development, and automated tests prove the seeded role data and role-switch authorization behavior.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-03 | Development uses EF Core InMemory only when `DevelopmentTesting:Enabled` and `IHostEnvironment.IsDevelopment()` are both true. Deterministic non-sensitive identity, claim, collection, job-payment, payroll, notification, audit, and OAuth-client data is seeded; the sign-in page offers User/Teller/Manager/Accountant/Administrator role selection while Blocked remains inactive. Files: `src/ElixomClaim.Lib/DependencyInjection.cs`, `src/ElixomClaim.Web/Development/DevelopmentDataSeeder.cs`, `src/ElixomClaim.Web/Controllers/AccountController.cs`, `src/ElixomClaim.Web.Tests/Development/DevelopmentDataSeederTests.cs`. Commands: `dotnet test ElixomClaim.slnx --no-restore` passed (130 tests); `git diff --check` passed. |
