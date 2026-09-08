# Literal Audit and Email Record Model

- **Status:** Accepted
- **Date:** 2026-09-08
- **Author:** Product / Engineering

## Context

`AuditRecord` combines a target and timestamp, and notification records use one recipient with system-copy messages delivered separately. These are delivery-equivalent variations but do not literally preserve source-specified audit fields or email header semantics.

## Decision

- Persist audit `EntityType`, `EntityId`, and `OccurredAtUtc` as separate fields; Azure SQL append-only protection remains mandatory and historical target data is retained through a documented migration.
- Persist `To`, `From`, `Cc`, and `Bcc` for queued/logged messages. The configured system-copy address is always Bcc, never Cc, and is never exposed in recipient-visible content, previews, unauthorized queries, diagnostics, audit payloads, or logs.
- Managers may list metadata-only audit records for claims, collections, and job payments only. Payroll, salary, user-administration, OAuth/security, email-delivery, and all other audit domains are Administrator-only; before/after state is Administrator-only in every domain.
- Keep the durable outbox, retries, idempotency, approved-template/recipient limits, provider abstraction, redaction, and delivery-attempt evidence. The new model must not enable arbitrary recipients, free-form/bulk email, direct-provider sending, or worker invocation.

## Consequences

Sprint 13 must make a data-preserving Azure SQL migration and update mappings, composition/sender paths, audit queries, projections, and relational/integration tests. Bcc privacy receives explicit coverage.
