# ADR 0001: In-house OAuth 2.0 authorization server for MCP

- **Status:** Accepted
- **Date:** 2026-09-02

## Context

MCP clients need delegated access as a concrete Elixom Claim user. The product requires the application to own its OAuth 2.0 authorization-server implementation, including dynamic client registration, authorization, consent, refresh tokens, revocation, and audit trails.

## Decision

Implement OAuth 2.0 endpoints in `ElixomClaim.Web`: dynamic client registration, authorization, token, and revocation. Support Authorization Code with mandatory PKCE S256. Persist client registrations, consent, authorization codes, refresh-token families, revocations, scopes, and security/audit events in the application database. Resolve access tokens to an application user and execute MCP tools under that user's policies.

The implementation uses .NET platform cryptographic primitives, secure random generation, constant-time secret comparison, key rotation, hashed storage for credentials/codes/refresh tokens where applicable, exact redirect URI matching, short expirations, replay detection, rate limiting, and structured redacted audit events. It must not invent cryptographic algorithms.

## Consequences

The application owns a high-risk protocol surface and must maintain its standards compatibility, threat model, regression suite, key management, incident runbook, and independent security review. Dynamic registration needs an explicit admission/authentication policy to prevent uncontrolled client creation.

## Verification required

- Threat model completed before production.
- Automated protocol/interoperability and negative-security tests for registration, redirect URI matching, consent, PKCE, code/refresh replay, revocation, scopes, expiry, and user-role projection.
- Independent security review before production launch and after material protocol changes.

## Related

- `README.md` — Security, OAuth, and MCP
- `sprints/01-identity-security.md`
- `sprints/06-mcp-release.md`
