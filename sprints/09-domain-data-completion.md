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
| 1 | Complete | 2026-09-03 | Implemented ADR 0004 all-Guid primary/foreign keys across entities, services, controllers, routes ({id:guid}), MCP tools, seeder, and tests; build & 145 tests passed. |
| 2 | Complete | 2026-09-03 | Added CreatedAtUtc to CollectionPurposeOption, CollectionAmountOption, SalaryAdjustment; SentAtUtc to EmailLog; DateOfJob & DeletedAtUtc to Claim with ApplicationDbContext EF mappings and ClaimService/OutboxService logic; build & 150 tests passed. |
| 3 | Complete | 2026-09-03 | Added User.BankAccountName, User.BankName, GetMaskedBankAccountNumber, and UserProfileSummary safe bank projection. |
| 4 | Complete | 2026-09-03 | Added CollectionClient Description, Notes, PerJobProcessingFee, PerTransactionFee, CollectionClientBankDetail Notes, and CollectionTransaction PayorTelephone. |
| 5 | Complete | 2026-09-03 | Added JobPayment Title, PublicDescription, and PayoutBankName/PayoutBankAccountName/PayoutBankAccountNumber/PayoutBankBranchCode payout snapshots. |
| 6 | Complete | 2026-09-03 | Applied DataAnnotations & EF mappings, created migration 20260903120000_DomainDataCompletion, and added DomainDataCompletionRelationalTests; build & 159 tests passed. |
