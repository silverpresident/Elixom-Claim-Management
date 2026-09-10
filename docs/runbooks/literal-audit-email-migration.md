# Literal Audit and Email Migration Rehearsal Runbook

## Purpose and release gate

This runbook rehearses the clean initial baseline required by ADR 0007 and [ADR 0009](../../adr/0009-clean-slate-migration-baseline.md). It is mandatory before first production deployment. It must be executed by the designated production migration owner against a production-shaped Azure SQL environment using safe, non-delivery email configuration.

Do not run this process against production until the rehearsal evidence is accepted. Do not delete, truncate, or manually rewrite audit, financial, email, or outbox rows.

## Required inputs

- An empty Azure SQL database in the production-shaped staging environment, isolated from production applications and providers.
- A named migration runner and an independent observer.
- The reviewed EF migration script and its SHA-256 checksum.
- A deployment build configured with a safe email provider/test address and no production credentials.
- A backup/PITR timestamp taken immediately before rehearsal.

## Preflight

1. Record the restore source, UTC restore timestamp, database name, runner, observer, application build SHA, migration-script checksum, and ticket/change record. Do not put credentials, bank details, recipients, or message bodies in the evidence.
2. Verify that no prior application migration ledger or `dbclaim` application tables exist. This clean baseline must never be applied to a database with application data.

3. Generate and review the idempotent script. Confirm it creates `dbclaim`, literal fields/indexes, sequences, and the append-only audit trigger directly:

   ```bash
   dotnet ef migrations script --idempotent \
     --project src/ElixomClaim.Lib --startup-project src/ElixomClaim.Web \
     --output literal-audit-email.sql
   sha256sum literal-audit-email.sql
   ```

## Rehearsal execution

1. Stop all application instances connected to the staging database. Run exactly one migration runner.
2. Apply the reviewed script; retain its terminal output with secrets redacted.
3. Run the migration again to prove idempotence. It must make no schema/data changes.
4. Start one application instance with safe notification configuration. Create one approved collection receipt and one approved payout summary using synthetic/rehearsal records only.
5. Dispatch through the normal durable outbox worker. Do not call SMTP/ACS directly and do not use MCP/API to invoke worker internals.

## Validation

Run these checks and retain counts/results only:

```sql
SELECT COUNT(*) AS MissingAuditFields
FROM dbclaim.AuditRecords
WHERE EntityType IS NULL OR EntityId IS NULL OR OccurredAtUtc IS NULL;

SELECT COUNT(*) AS MissingHeaders
FROM dbclaim.EmailOutboxItems
WHERE [To] IS NULL OR [From] IS NULL;

SELECT COUNT(*) AS SystemCopyInCc
FROM dbclaim.EmailOutboxItems
WHERE Cc IS NOT NULL AND Cc LIKE '%' + @SystemCopyAddress + '%';

SELECT COUNT(*) AS TriggerCount
FROM sys.triggers
WHERE name = N'TR_AuditRecords_PreventMutation'
  AND parent_id = OBJECT_ID(N'dbclaim.AuditRecords');
```

All expected count checks are zero except `TriggerCount`, which is one. Also prove:

- an attempted `UPDATE` and `DELETE` of a synthetic audit row fail due to the append-only trigger;
- a recipient-visible receipt/payout body and redacted API/MCP preview contain no Bcc value;
- a Manager receives only permitted audit metadata, while an Administrator receives the authorized full audit projection;
- email attempt history remains attached to the same outbox item, retries remain idempotent, and valid recipients still deliver when an optional payor email is invalid;
- no queued/logged message has an arbitrary recipient or free-form body.

## Rollback and recovery

Stop the rehearsal application immediately if migration, validation, or append-only checks fail. Do not execute a destructive EF `Down` migration against a database with application data.

1. Preserve the failed staging database and migration logs for diagnosis.
2. Revert the application deployment to the prior build.
3. For production, restore the pre-deployment PITR backup to a new database as described in [backup and disaster recovery](backup-restore.md), validate counts, and switch only under the release owner's approval.
4. Record the failure, scope, data-integrity assessment, and remediation ticket. Rehearse the corrected script on a new restore.

## Evidence and sign-off

The release record must include the pre/post counts, script checksum, idempotence result, trigger check, safe end-to-end notification result, Bcc-redaction result, Manager/Administrator projection result, retry/recovery result, backup-restore reference, runner/observer names, UTC timestamps, and release-owner go/no-go decision. Exclude secrets, personal data, bank data, OAuth tokens, and message bodies.
