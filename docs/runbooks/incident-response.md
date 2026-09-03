# Security & Incident Response Runbook

## Severity Levels
- **Severity 1 (Critical):** Data breach, active compromise of OAuth tokens or user accounts, unauthorized financial mutation.
- **Severity 2 (High):** Component outage (e.g. email notification failure, database connection pool exhaustion).
- **Severity 3 (Medium):** Minor bug or degraded non-critical background hosted service.

## Immediate Containment Actions for Compromised User / MCP Client
1. Block compromised user account immediately in UI (`/admin/users`) or SQL:
   ```sql
   UPDATE dbclaim.Users SET Role = 'Blocked', IsActive = 0 WHERE Id = @UserId;
   ```
2. Revoke all active OAuth tokens for the user:
   ```sql
   UPDATE dbclaim.OAuthTokens SET IsRevoked = 1 WHERE UserId = @UserIdStr AND IsRevoked = 0;
   ```
3. If an OAuth client is compromised, deactivate the client:
   ```sql
   UPDATE dbclaim.OAuthClients SET IsActive = 0 WHERE ClientId = @ClientId;
   ```

## Post-Incident Escalation
1. Notify security officer and legal team (`privacy@elixom.com`).
2. Collect audit records matching correlation IDs:
   ```sql
   SELECT * FROM dbclaim.AuditRecords WHERE CorrelationId = @CorrelationId;
   ```
3. Document root cause analysis (RCA) and mitigation plan.
