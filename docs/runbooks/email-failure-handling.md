# Email Failure Handling & Outbox Maintenance Runbook

## Overview
All financial notifications and receipts are processed asynchronously through `dbclaim.EmailOutboxItems` and dispatched by `OutboxDispatchHostedService`. Delivery logs are recorded in `dbclaim.EmailLogs`.

## Failure Classification
1. **Skipped Invalid Recipient (`SkippedInvalidRecipient`):** Caused by missing or malformed email addresses (e.g. optional payor email). This outcome is recorded as non-blocking and does not stop delivery to other template recipients or system copies.
2. **Pending Retry (`Pending`):** Temporary SMTP or Azure Communication Services (ACS) transport failures automatically retry with exponential backoff up to 5 attempts.
3. **Failed (`Failed`):** Permanent failure after 5 unsuccessful attempts.

## Troubleshooting & Manual Outbox Wake-up
1. Check stuck or failed outbox items:
   ```sql
   SELECT Id, Recipient, Subject, Status, AttemptCount, FailureReason, CreatedAtUtc
   FROM dbclaim.EmailOutboxItems
   WHERE Status IN ('Failed', 'Pending')
   ORDER BY CreatedAtUtc DESC;
   ```
2. Re-trigger outbox dispatch via MCP Operations tool:
   - Call `POST /mcp/operations/outbox-wakeup` with Administrator token and `{"batchSize": 50, "idempotencyKey": "manual-retry-001"}`.
3. Reset failed outbox items for retry after provider resolution:
   ```sql
   UPDATE dbclaim.EmailOutboxItems
   SET Status = 'Pending', AttemptCount = 0, AvailableAtUtc = GETUTCDATE()
   WHERE Status = 'Failed' AND CreatedAtUtc > DATEADD(day, -7, GETUTCDATE());
   ```
