# Audit Record Database Immutability

- **Status:** Accepted
- **Date:** 2026-09-03
- **Author:** Engineering

## Context

Audit records are security and financial accountability evidence. Application-level conventions alone cannot prevent an accidental or compromised application path from updating or deleting an existing record.

## Decision

Azure SQL deployments create `[dbclaim].[TR_AuditRecords_PreventMutation]` through EF migration `20260903090000_AddAuditRecordAppendOnlyTrigger`. The trigger rejects every `UPDATE` and `DELETE` against `[dbclaim].[AuditRecords]`; inserts remain permitted for the audit service. The migration's down path removes the trigger only when an explicit migration rollback is performed.

The application database principal must not have permission to disable, alter, or drop this trigger. Any exceptional correction requires a reviewed, privileged database change and a separate audit trail outside the affected table.

## Consequences

### Positive

- The append-only invariant survives application bugs and direct ordinary DML access.
- The enforcement applies to all application adapters, including MVC, workers, and MCP.

### Negative / Trade-offs

- Retention or data-remediation operations cannot delete audit rows through ordinary application access.
- Highly privileged database administrators can still alter database objects, so production access control and audit review remain necessary.

## Security & Compliance

The trigger error contains no record content or sensitive values. Production migration authority must verify the trigger after each database creation or migration recovery; its presence is part of the release-security checklist.
