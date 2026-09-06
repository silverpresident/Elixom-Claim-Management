# Database Migration and Rollback Operations Guide

## Overview

ElixomClaim uses EF Core Code First migrations targeting Azure SQL under schema `dbclaim`.
Database schema migrations are non-destructive and managed via `ApplyDatabaseMigrationsAsync()` in `ElixomClaim.Lib.Data`.

## Deployment Topology & Migration Authority

### Production Mode
1. **Guarded Application Startup (Default):**
   - When the web application starts up in non-Development mode (or with `DevelopmentTesting:Enabled` = `false`), `ApplyDatabaseMigrationsAsync()` executes during startup.
   - Concurrency is protected via `SemaphoreSlim` in-process lock and EF Core's built-in `__EFMigrationsHistory` transaction locks in SQL Server.
   - Configuration guard: Set `ConnectionStrings:AutoApplyMigrations` to `false` in configuration to disable automated startup migrations if a dedicated pipeline task is preferred.

2. **Dedicated Deployment Migration Job:**
   - For blue/green or multi-instance container deployments, disable `AutoApplyMigrations` in web instances (`"AutoApplyMigrations": false`).
   - Run a single migration container or CI/CD step before routing traffic:
     ```bash
     dotnet ef database update --project src/ElixomClaim.Lib --startup-project src/ElixomClaim.Web --connection "<ConnectionString>"
     ```

## Health and Readiness Behavior

- `/health/live`: Liveness endpoint. Returns `200 OK` if the web application host is running.
- `/health/ready`: Readiness endpoint. Verifies database connectivity using EF Core's `ApplicationDbContext` health check. Returns `503 Service Unavailable` if database connection or migrations fail.

## Rollback & Failure Recovery Procedure

### Startup Migration Failure
If `ApplyDatabaseMigrationsAsync()` encounters an exception during application startup:
1. The error is logged with sensitive connection credentials redacted.
2. The application startup fails fast and terminates the process, preventing unmigrated web instances from servicing HTTP requests.
3. `/health/ready` will not pass on unhealthy instances.

### Schema Rollback Procedure
1. EF Core migrations in ElixomClaim are designed to be non-destructive (adding new tables/columns or triggers without removing existing data).
2. To inspect SQL statements before applying:
   ```bash
   dotnet ef migrations script <SourceMigration> <TargetMigration> --project src/ElixomClaim.Lib --startup-project src/ElixomClaim.Web
   ```
3. To revert to a previous migration in a non-production environment:
   ```bash
   dotnet ef database update <TargetMigrationName> --project src/ElixomClaim.Lib --startup-project src/ElixomClaim.Web
   ```
4. In production environments:
   - Rollbacks should be performed using verified SQL rollback scripts reviewed by DBA.
   - Immutable audit triggers (`TR_AuditRecords_PreventMutation`) must remain enforced at all times.
