# Bootstrap Administrator Recovery Runbook

## Overview
Elixom Claim uses Google OpenID Connect for authentication and provisions administrative access via a configured default administrator email address (`Authentication:DefaultAdminEmail`).

## Emergency Scenario: Administrator Lockout
If administrative access is lost or all active Administrator accounts are inadvertently blocked/deactivated:

1. Confirm the target administrator Google email address (e.g., `admin@elixom.com`).
2. Update the `Authentication:DefaultAdminEmail` configuration setting in Azure Key Vault or Environment Variables:
   ```bash
   Authentication__DefaultAdminEmail="admin@elixom.com"
   ```
3. Restart the ASP.NET Core Web Application instance.
4. On application startup, `UserValidationEvents` and seed initialization detect `DefaultAdminEmail`.
5. If the user record exists, its `Role` is promoted to `Administrator` and `IsActive` is set to `true`. If no user record exists, a new active `Administrator` record is created.
6. The administrator signs in using Google OpenID Connect with `admin@elixom.com`.
7. Verify access to `/admin/users` and log the recovery event in the operational log.
