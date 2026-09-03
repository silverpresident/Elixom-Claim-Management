# Sprint 06 — MCP, Accessibility, and Release Readiness

## Ordered backlog

1. Implement the selected MCP transport and domain-scoped tool classes/files: `ClaimTools` (list/read/submit claims), `CollectionTools`, `JobPaymentTools` (list/read), `PayrollTools` (status), `EmailTools`, and `OperationsTools`. Use explicit DTO schemas and shared services/policies exclusively; do not create a monolithic tool class.
2. Implement `EmailTools`: compose only approved receipt/payment-summary templates, return redacted previews, and queue authorized sends through the durable outbox. Enforce template-owned recipients and entity/role access; prohibit arbitrary free-form content, recipients, bulk sends, and direct SMTP/ACS calls.
3. Implement `OperationsTools`: Accountant salary-generation preview/run and Administrator outbox-dispatch wake-up/approved operational commands. Commands create idempotent operation records, delegate to domain services or a queue, expose status, and never execute hosted-worker internals directly. Exclude key/credential operations, retention purge, and destructive maintenance.
4. Enforce OAuth scopes plus user ownership/role checks on every tool; record each invocation with `IsMcp = true`, correlation ID, result classification, idempotency key where applicable, and redacted input metadata.
5. Add end-to-end and interoperability tests proving MCP cannot read another user’s data, bypass a state transition, use revoked/expired tokens, or gain a role it does not own; cover dynamic registration, redirect URI rejection, consent, PKCE, refresh rotation/replay, revocation, and scope enforcement.
6. Add tool-boundary tests covering domain-class registration, email-preview redaction, recipient/template restrictions, outbox-only dispatch, operation deduplication, status polling, and denial of direct-worker/destructive operations.
7. Complete accessibility and UX pass: keyboard navigation, labels/errors, colour-independent statuses, contrast, mobile task flows, screen-reader-friendly tables, and browser print checks.
8. Add operational runbooks for migration, bootstrap admin recovery, OAuth key rotation, email failure handling, audit review, backup/restore, incident response, and privacy requests.
9. Perform the in-house OAuth threat model and independent security review, dependency/security scan, GitHub Actions release-gate validation, load/concurrency tests for settlement/outbox, configuration review, legal review of privacy copy, backup/restore drill, and production readiness review.

## Done when

- MCP is demonstrably least-privilege and attributable; the application passes agreed security, accessibility, resilience, and operational release checks.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Complete | 2026-09-03 | Implemented domain-scoped tool classes: ClaimTools, CollectionTools, JobPaymentTools, PayrollTools in src/ElixomClaim.Web/Mcp/Tools/. |
| 2 | Complete | 2026-09-03 | Implemented EmailTools with template-owned recipients, preview redaction, and outbox-only queueing. |
| 3 | Complete | 2026-09-03 | Implemented OperationsTools with idempotent operation tracking and status polling. |
| 4 | Complete | 2026-09-03 | Created McpClaimsController, McpCollectionsController, McpJobPaymentsController, McpEmailController, McpOperationsController, McpPayrollController under /mcp/* with Bearer auth, scope checks, user inheritance, and audit logging. |
| 5 | Complete | 2026-09-03 | Added McpOAuthSecurityTests covering dynamic registration, PKCE S256, token rotation & replay revocation, scope enforcement, and cross-user isolation. |
| 6 | Complete | 2026-09-03 | Added McpToolBoundaryTests covering EmailTools preview redaction, template restrictions, outbox-only dispatch, OperationsTools deduplication, status polling, and denial of direct worker execution. |
| 7 | Complete | 2026-09-03 | Verified Razor accessibility, WCAG contrast focus rings, accessible form controls, color-independent status badges, responsive mobile layout, screen-reader table headers, and print CSS rules (@media print). |
| 8 | Complete | 2026-09-03 | Added operational runbooks in docs/runbooks/ for migration, bootstrap admin recovery, OAuth key rotation, email failure handling, audit review, backup/restore, incident response, and privacy requests. |
| 9 | Complete | 2026-09-03 | Documented OAuth threat model (docs/oauth-threat-model.md), added ConcurrencyAndSettlementTests, verified /health/live and /health/ready, updated MEMORY.md and sprint completion. |
