# Sprint 01 — Identity, Authorization, Audit, and OAuth Foundation

## Ordered backlog

1. Model `User`, hierarchical role enum, active/blocked state, profile/bank fields, unique normalized email, and safe bootstrap-admin seeding.
2. Configure Google OpenID Connect sign-in/cookie lifecycle; reject unknown/inactive users with a clear not-provisioned experience; refresh role claims at login.
3. Add policy/ownership services and test every role boundary, including Blocked access and inherited capabilities.
4. Model append-only audit records and implement a redacting audit service with actor, correlation, IP, action, target, before/after, and MCP flag.
5. Build the OAuth authorization-server foundation using a reviewed implementation approach: registered clients, exact redirect URI validation, consent, authorization code storage, PKCE S256 verification, short-lived tokens, refresh rotation/revocation, scopes, throttling, and audit events.
6. Implement bearer-token validation middleware that projects the resolved application user into `HttpContext.User`; test expired, revoked, wrong-client, and scope/role failure paths.
7. Add Admin user management and the Manager/Admin audit views with final agreed visibility scope.

## Done when

- An allow-listed active Google user can sign in; an unprovisioned or blocked identity cannot.
- OAuth and application security events are auditable without secrets, and authorization tests demonstrate no privilege escalation.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | Not started | — | — |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
| 6 | Not started | — | — |
| 7 | Not started | — | — |
