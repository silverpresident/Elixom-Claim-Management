# Audit Review & Security Operations Runbook

## Overview
All data mutations, authentication events, OAuth issuance/revocation, and MCP operations write append-only audit records to `dbclaim.AuditRecords`.

## Querying Audit Logs
1. View recent MCP operations:
   ```sql
   SELECT Id, TimestampUtc, Action, Target, ActorUserId, CorrelationId, IpAddress
   FROM dbclaim.AuditRecords
   WHERE IsMcpOperation = 1
   ORDER BY TimestampUtc DESC;
   ```
2. Inspect financial mutations for a job payment:
   ```sql
   SELECT TimestampUtc, Action, ActorUserId, BeforeStateJson, AfterStateJson
   FROM dbclaim.AuditRecords
   WHERE Target = 'JobPayment:1001'
   ORDER BY TimestampUtc ASC;
   ```

## Sensitive Data Verification
Confirm that audit payloads do not contain:
- Unredacted bank account numbers or routing codes
- OAuth client secrets or access/refresh tokens
- Google OAuth credentials
- Unredacted financial email body tokens
