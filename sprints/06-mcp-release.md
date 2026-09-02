# Sprint 06 — MCP, Accessibility, and Release Readiness

## Ordered backlog

1. Implement the selected MCP transport and a small initial tool set: list/read claims, submit a claim, list job payments, and read payroll status. Reuse shared services and policies exclusively.
2. Enforce OAuth scopes plus user ownership/role checks on every tool; record each invocation with `IsMcp = true`, correlation ID, result classification, and redacted input metadata.
3. Add end-to-end tests proving MCP cannot read another user’s data, bypass a state transition, use revoked/expired tokens, or gain a role it does not own.
4. Complete accessibility and UX pass: keyboard navigation, labels/errors, colour-independent statuses, contrast, mobile task flows, screen-reader-friendly tables, and browser print checks.
5. Add operational runbooks for migration, bootstrap admin recovery, OAuth key rotation, email failure handling, audit review, backup/restore, incident response, and privacy requests.
6. Perform threat model, dependency/security scan, load/concurrency tests for settlement/outbox, configuration review, legal review of privacy copy, and production readiness review.

## Done when

- MCP is demonstrably least-privilege and attributable; the application passes agreed security, accessibility, resilience, and operational release checks.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Not started | — | — |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
