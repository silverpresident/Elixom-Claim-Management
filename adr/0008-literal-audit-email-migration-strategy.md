# Literal Audit and Email Migration Strategy

- **Status:** Accepted
- **Date:** 2026-09-08
- **Author:** Engineering
- **Supersedes:** None; implements [ADR 0007](0007-literal-audit-and-email-record-model.md)

## Context

ADR 0007 selected literal persistence for audit targets and email headers. The deployed model currently stores audit `Target`/`TimestampUtc` and one `Recipient` per durable outbox/log row. Financial, audit, email, and outbox history must be retained, and `AuditRecords` is protected by an Azure SQL trigger that rejects updates and deletes.

## Decision

The release migration is additive and data-preserving:

| Existing field | Literal field(s) | Historical backfill |
| --- | --- | --- |
| `AuditRecords.Target` | `EntityType`, `EntityId` | Split the first `:` only. A target without a separator becomes `EntityType = "Legacy"`, `EntityId = Target`; neither source value is discarded during the release migration. |
| `AuditRecords.TimestampUtc` | `OccurredAtUtc` | Copy exactly, in UTC. |
| `EmailOutboxItems.Recipient` | `To` | Copy exactly. Each existing durable row remains one delivery recipient. |
| `EmailLogs.Recipient` | `To` | Copy exactly. Each attempt remains linked to its existing outbox row. |
| configured `Notifications:FromAddress` | `From` | Stamp every newly queued message; backfill historical rows with a documented migration sentinel only when the actual historical sender is unavailable. |
| no source field | `Cc`, `Bcc` | Backfill empty. New system-copy recipients are stored only in `Bcc`; they are never added to `Cc`. |

`EntityType`, `EntityId`, and `OccurredAtUtc` become non-null after backfill. `To` and `From` become non-null for new queued/logged records; `Cc` and `Bcc` are optional. The append-only trigger remains in place. It must be dropped and recreated only inside the reviewed migration transaction when table shape changes require it; no historic audit row may be updated after the trigger is restored.

New indexes support the authorized query paths: `AuditRecords(EntityType, OccurredAtUtc DESC)`, `AuditRecords(EntityId, OccurredAtUtc DESC)`, and retained outbox/log status, relation, and creation-time indexes. Header fields are never used to broaden recipients or authorization.

## Consequences

- Lib services and transport adapters receive structured entity/header arguments and must not recreate string parsing or recipient selection.
- Authorized projections expose only the minimum header metadata for the caller. Bcc is never returned to a recipient, Manager, API/MCP preview, rendered HTML, audit payload, application log, or error.
- The system-copy address is a single Bcc recipient on the logical message. It does not create a recipient-visible Cc header.
- Existing source columns remain for one release only as compatibility evidence. A later, separately approved migration may remove them after a verified backup/restore and retention review; it is not part of Sprint 13.
- A restore rehearsal is required before production execution; the procedure is in [the literal-model migration runbook](../docs/runbooks/literal-audit-email-migration.md).
