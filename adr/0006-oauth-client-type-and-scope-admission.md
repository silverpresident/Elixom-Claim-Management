# OAuth Client Type and Scope Admission Policy

- **Status:** Accepted
- **Date:** 2026-09-08
- **Author:** Engineering

## Context

The authorization server persisted each OAuth client's allowed scopes but did not enforce them during authorization or consent. It also generated and returned a client secret for every dynamic registration while allowing a client to omit that secret during token and refresh exchanges. This made the public-versus-confidential client contract ambiguous.

## Decision

- Dynamic registration at `/oauth/register` creates public MCP clients only. It returns no client secret, requires authorization-code PKCE S256, and allows `openid`, `profile`, `email`, and `mcp:access` only.
- Every requested scope is compared with the concrete client's persisted `AllowedScopes` before consent or authorization-code issuance. Service-layer operations repeat this validation so no adapter can mint a code for an unallowed scope.
- `OAuthClient.ClientType` is persisted as `Public` or `Confidential`. Public clients must not provide a secret. Confidential clients are trusted, administratively provisioned integrations and must present a valid stored-secret hash at authorization-code exchange and refresh.
- Existing client records migrate as `Confidential` to preserve their credentialed behavior.

## Consequences

MCP clients have an explicit interoperable public-client policy and cannot obtain arbitrary transport scopes. Confidential-client provisioning is intentionally not exposed through anonymous dynamic registration. A future retained REST API needs a separately trusted provisioning path and a client allow-list that includes `api:access`.
