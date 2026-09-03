# Database Migration Runbook

## Overview
All database schema objects reside exclusively in the Azure SQL schema `dbclaim`. EF Core migrations manage schema changes.

## Guiding Principles
- Automatic destructive schema operations (e.g., dropping columns/tables without explicit review) are strictly prohibited.
- Production migrations must run through a single-instance migration job (`ApplyDatabaseMigrationsAsync()`) during deployment windows to prevent concurrent execution races.

## Developer Execution (Local Environment)
1. Configure local SQL Server connection string in user secrets:
   ```bash
   dotnet user-secrets set "ConnectionStrings:ClaimDatabase" "Server=(localdb)\mssqllocaldb;Database=ElixomClaimDb;Trusted_Connection=True;MultipleActiveResultSets=true" --project src/ElixomClaim.Web
   ```
2. Apply migrations locally:
   ```bash
   dotnet ef database update --project src/ElixomClaim.Lib --startup-project src/ElixomClaim.Web
   ```

## Production Execution Pipeline
1. Verify migration script generation:
   ```bash
   dotnet ef migrations script --project src/ElixomClaim.Lib --startup-project src/ElixomClaim.Web --idempotent --output migration.sql
   ```
2. Inspect `migration.sql` to confirm all target statements use schema `dbclaim` and contain no destructive `DROP` statements without backup.
3. Deploy during scheduled maintenance window using single-runner execution.

## Rollback Procedure
If a migration fails during deployment:
1. Stop the application service instance.
2. Revert to the prior application build image.
3. If database rollback is necessary, restore from Azure SQL Point-in-Time Restore (PITR) snapshot taken immediately prior to deployment.
