# In-House OAuth 2.0 Threat Model & Security Review

## System Scope
The in-house OAuth 2.0 authorization server provides client registration, Authorization Code + PKCE S256 authorization, access token validation, and refresh token rotation with family revocation for MCP client integrations.

## Threats & Mitigation Analysis

| Threat ID | Threat Description | Attack Vector | Security Controls & Mitigation | Status |
| --- | --- | --- | --- | --- |
| **TH-01** | Authorization Code Interception / Misdirection | Attacker intercepts authorization code in transit or via custom URI scheme. | Mandatory PKCE S256 verification (`code_challenge` / `code_verifier`). Exact string matching on registered `redirect_uris`. | **Mitigated** |
| **TH-02** | Refresh Token Replay / Theft | Attacker steals a valid refresh token and attempts token exchange. | Refresh token rotation on every use. Replay of an already revoked/used refresh token immediately triggers revocation of the entire token family (`RefreshTokenFamilyId`). | **Mitigated** |
| **TH-03** | Scope Escalation / Privilege Escalation | Attacker attempts to request unauthorized administrative scopes or execute elevated commands. | OAuth tokens inherit concrete user identity (`UserId`). Scope policy checks (`mcp:access`) plus domain service role authorization (`UserRole`) are enforced at every endpoint. MCP clients cannot gain roles higher than the logged-in user. | **Mitigated** |
| **TH-04** | Client Impersonation | Malicious entity poses as a registered MCP client. | Cryptographic client registration (`client_id` + SHA-256 hashed `client_secret`). Direct client secret storage in plain text is prohibited. | **Mitigated** |
| **TH-05** | Token Theft via Storage Leak | Exposed access tokens in application logs or audit records. | Structured log redaction (`ILogger<T>`). Access token hashes stored in database; raw access tokens never logged in `AuditRecords` or `EmailLogs`. Short-lived access tokens (1 hour). | **Mitigated** |
| **TH-06** | Open Redirect Vulnerability | Attacker passes malicious `redirect_uri` in `/oauth/authorize`. | Strictly validated against exact pre-registered client `RedirectUrisJson`. Unregistered URIs are rejected immediately. | **Mitigated** |

## Summary Recommendation
The in-house OAuth 2.0 implementation satisfies RFC 6749 and RFC 7636 (PKCE) security directives.
