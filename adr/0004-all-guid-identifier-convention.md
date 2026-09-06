# All-Guid Identifier Convention Across Domain Entities

- **Status:** Accepted
- **Date:** 2026-09-03
- **Author:** Engineering

## Context

The system previously used a mix of auto-incrementing integer (`long`) identifiers and globally unique identifiers (`Guid`) for entity primary keys and foreign keys. This mixture created inconsistency in REST route patterns, MCP DTO contracts, distributed client resource identification, and database relational schema design. To support unambiguous global identity, uniform Web and MCP API contracts, and lock-free ID generation across distributed services, a single primary key convention is required.

## Decision

All persisted entity primary keys and foreign key references across the `dbclaim` schema will standardize on `Guid` (128-bit globally unique identifiers).

Specifically:
- Domain entities (`Claim`, `ClaimComment`, `AuditRecord`, `CollectionClientBankDetail`, `CollectionPurposeOption`, `CollectionAmountOption`, `CollectionTransaction`, `SalaryDefinition`, `SalaryAdjustment`, `Payroll`, `PayrollEntry`, `JobPayment`, `JobPaymentDeduction`, `EmailLog`, `OperationRecord`) convert their `long Id` and associated `long` foreign keys to `Guid`.
- Entity composite key join tables (`JobPaymentClaim`, `JobPaymentCollection`, `JobPaymentPayroll`, `CollectionClientUser`) utilize `Guid` foreign key properties.
- EF Core configurations set `Guid` primary keys with `SequentialGuidValueGenerator` to maintain index efficiency on relational database engines.
- DTO contracts, MVC route parameters (`{id:guid}`), MCP tool payload definitions, development seeders, and test suites adopt `Guid` identifiers.

### Migration & Reset Strategy

Given the system is in active pre-production development with an EF Core baseline reset, existing EF Core migration snapshots and baseline migration definitions will be updated to target `uniqueidentifier` (`Guid`) columns for all entity primary and foreign keys in Azure SQL.

## Consequences

### Positive

- Identifiers can be safely generated client-side or in application memory prior to persistence without sequence collisions.
- Unified routing and DTO contracts simplify API design across MVC, Razor, and MCP protocols.
- Obfuscates sequential record enumeration and prevents indirect object reference discovery.

### Negative / Trade-offs

- `Guid` primary keys consume 16 bytes compared to 8 bytes for `bigint`, resulting in slightly larger index storage footprints.
- URLs and logs display longer string representations (36 characters).

## Security & Compliance

- Prevents record enumeration attacks via sequential integer IDs.
- Audit records continue to capture entity identifiers and correlation IDs as string representations, maintaining full traceability.
