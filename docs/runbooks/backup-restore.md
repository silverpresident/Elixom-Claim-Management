# Backup & Disaster Recovery Runbook

## Overview
Elixom Claim utilizes Azure SQL Server automated backups with Point-in-Time Restore (PITR) and Geo-Redundant Backup storage.

## Backup Retention Policy
- Short-term PITR retention: 35 days (1-minute RPO).
- Long-term retention (LTR): Weekly and monthly backups retained for 9 years to meet legal and financial compliance requirements.

## Database Restoration Procedure
1. Identify target restoration timestamp (UTC).
2. Restore database to new Azure SQL instance:
   ```bash
   az sql db restore --resource-group elixom-rg \
     --server elixom-sql-server \
     --name ElixomClaimDb \
     --dest-name ElixomClaimDb-Restored \
     --time "2026-09-03T12:00:00Z"
   ```
3. Update connection string in Azure Key Vault / App Service configuration.
4. Verify database health check endpoint `GET /health/ready`.
5. Conduct data validation queries on schema `dbclaim`.
