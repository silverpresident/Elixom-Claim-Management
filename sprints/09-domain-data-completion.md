# Sprint 09 — Domain Data Completion and Persistence Consistency

## Prerequisites

- Sprint 08 must be complete.
- Add an ADR before converting identifier types. The approved target is `Guid` for all persisted entity identifiers; the ADR must state migration/reset and compatibility strategy before any schema edit.

## Ordered backlog

1. Establish and implement the approved all-`Guid` identifier convention across entities, foreign keys, service DTOs, routes, seed data, and tests. Preserve referential integrity and record the schema migration/reset strategy in the ADR and `MEMORY.md`.
2. Complete common auditability fields: add `CreatedAtUtc` to every non-join, non-aggregate entity that lacks it, add `SentAtUtc` to `EmailLog`, and add claim `DateOfJob` plus `DeletedAtUtc`. Enforce UTC/default/required semantics in mappings and tests.
3. Add ordinary-user profile and bank-data fields required by the specification, including bank name and account name, with sensitive-data-safe projections and validation.
4. Add clearing-house data fields: collection-client description, internal notes, per-job processing fee, per-transaction fee; internal notes on collection-client bank details; and optional internal-only payor telephone on collection transactions. Define fee snapshot/calculation behavior so later client edits cannot rewrite financial history.
5. Add job-payment title, public description (renaming `PublicNotes` through a backwards-safe migration), internal-note metadata, and payout bank snapshots (bank name, account name, account number, branch). Ensure payout summaries can use snapshots rather than mutable profile/client data.
6. Apply appropriate data annotations and EF mappings for display names, descriptions, column types, lengths, requiredness, and every monetary `decimal(18,2)` property. Create one coherent migration set and relational integration tests proving schema constraints, defaults, snapshots, soft deletion, and money precision.

## Done when

- The required domain data is persisted with a documented all-`Guid` identity convention, UTC audit timestamps, decimal precision, and relational constraints.
- Sensitive fields, fee/bank snapshots, and renamed public description are migration-safe and covered by relational integration tests.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Not started | — | Requires ADR before implementation. |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
