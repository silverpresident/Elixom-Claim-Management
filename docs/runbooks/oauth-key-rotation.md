# OAuth Key & Secret Rotation Runbook

## Overview
Elixom Claim operates an in-house OAuth 2.0 authorization server providing dynamic client registration, Authorization Code + PKCE S256, access tokens, and refresh token rotation with family revocation.

## Rotating Client Secrets
When an OAuth client secret is compromised or routine rotation is required:
1. Identify the target `ClientId`.
2. Generate a new secure 256-bit random client secret:
   ```csharp
   var newSecret = "secret_" + RandomNumberGenerator.GetHexString(32);
   ```
3. Store SHA-256 hash of `newSecret` in `dbclaim.OAuthClients.ClientSecretHash`.
4. Securely transmit `newSecret` to the client application administrator.
5. Invalidate existing active refresh tokens associated with `ClientId` if key compromise is suspected:
   ```sql
   UPDATE dbclaim.OAuthTokens SET IsRevoked = 1 WHERE ClientId = @ClientId AND IsRevoked = 0;
   ```

## Immediate Token Revocation Incident Procedure
To immediately revoke all tokens for a user or client:
1. Execute revocation via MCP API endpoint `POST /oauth/revoke` with parameter `token=<token>`.
2. Alternatively, revoke token family directly in SQL:
   ```sql
   UPDATE dbclaim.OAuthTokens SET IsRevoked = 1 WHERE RefreshTokenFamilyId = @FamilyId;
   ```
