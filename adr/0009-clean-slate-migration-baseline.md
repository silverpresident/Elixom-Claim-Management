# Clean-Slate Migration Baseline

- **Status:** Accepted
- **Date:** 2026-09-10
- **Author:** Engineering
- **Supersedes:** The migration delivery sequence in [ADR 0008](0008-literal-audit-email-migration-strategy.md) only where no database schema or data exists to preserve.

## Context

The repository has no deployed database, migration ledger, or regulated data to preserve. The former migration chain created a legacy recipient/target schema, then converted it to the literal audit and email-header model. Keeping that chain would make every first deployment run obsolete transitional schema steps and historical backfill logic that have no data to transform.

## Decision

Replace the complete EF migration chain with one generated `InitialCreate` migration based on the current model. It creates the `dbclaim` schema, sequences, literal audit fields (`EntityType`, `EntityId`, `OccurredAtUtc`), literal email headers (`To`, `From`, `Cc`, `Bcc`), indexes, constraints, and the append-only audit trigger directly.

This decision is valid only before any migration has been applied outside disposable local development databases. If a database or migration history later exists, it must not be pointed at this baseline; a reviewed data-preserving upgrade migration and rehearsal are required instead.

## Consequences

- First deployment has a single clean migration and no legacy `Target`, `TimestampUtc`, or `Recipient` columns.
- `TR_AuditRecords_PreventMutation` is created in the baseline and dropped before `AuditRecords` on a clean rollback.
- Existing ADR 0008 remains the required strategy for any future deployed-data transition; it is not rewritten as a historical decision.
