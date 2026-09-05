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
| **TH-06** | Open Redirect Vulnerability | Attacker passes malicious `redirect_uri` in `/oauth/authorize`. | Strictly validated against exact pre-registered client `RedirectUrisJson`. Unregistered URIs are rejected immediately. Dynamic client registration enforces strict scheme/host rules (max 10 URIs, HTTPS or HTTP on localhost/127.0.0.1, no fragments/wildcards). | **Mitigated** |
| **TH-07** | Endpoint Flooding / Denial of Service | Attacker floods `/oauth/*`, `/mcp`, or MVC endpoints. | Application-level rate limiting (`Microsoft.AspNetCore.RateLimiting`) with endpoint-specific policies (`oauth`: 20 req/min, `mcp`: 60 req/min, `mvc`: 100 req/min) using safe client partition keys (User ID or Remote IP) and safe 429 JSON responses. | **Mitigated** |
| **TH-08** | Consent Re-Prompt / Parameter Tampering | Attacker tampers with redirect URI or client ID during consent POST. | Parameter revalidation on `POST /oauth/authorize` prior to code generation. Consents are persisted in `dbclaim.OAuthConsents` to prevent re-prompts for authorized clients. | **Mitigated** |
| **TH-09** | Raw Authorization Code Exposure | Database or log leak exposes valid authorization codes. | Raw authorization codes are never retained in database or logs; only `CodeHash` (SHA-256) is stored in `dbclaim.OAuthAuthorizationCodes`. Codes expire in 5 minutes. | **Mitigated** |
| **TH-10** | Token Lifetime & Expiration Misconfiguration | Unbounded token lifetimes allow prolonged access after compromise. | Validated `OAuthOptions` with strict configurable lifetimes (3,600s access tokens, 30-day refresh tokens). Options validation rejects values below 60s or above 1 year. | **Mitigated** |
| **TH-11** | Operation State Loss on Restart | MCP operation tracking stored in memory lost on process restart. | Persisted in `dbclaim.OperationRecords` table with unique idempotency key constraint and audited operation service. | **Mitigated** |
| **TH-12** | Non-Standard Transport Vulnerabilities | Custom REST wrappers bypass standard MCP client security invariants. | Standard .NET MCP Server transport (`ModelContextProtocol.AspNetCore`) mapped at `/mcp` with mandatory Bearer authentication and `mcp:access` scope validation via `IMcpActorResolver`. | **Mitigated** |

## Summary Recommendation
The in-house OAuth 2.0 implementation satisfies RFC 6749, RFC 7636 (PKCE), and standard Model Context Protocol security directives.
