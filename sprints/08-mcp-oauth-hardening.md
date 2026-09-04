# Sprint 08 — MCP Transport and OAuth Hardening

## Prerequisites

- Sprint 01 item 4a must be complete first. `AuditRecords` must be immutable at the database boundary before this sprint relies on them for durable security and operation evidence.
- Record any material protocol, persistence, or client-compatibility choice in an ADR before implementation.

## Ordered backlog

1. Replace the bespoke bearer-authenticated `/mcp/*` REST adapter surface with a registered, standard .NET MCP server transport and register the existing domain-scoped tools (`ClaimTools`, `CollectionTools`, `JobPaymentTools`, `PayrollTools`, `EmailTools`, and `OperationsTools`) through that transport. Keep tools as thin adapters over shared authorization-aware Lib services; remove or retire redundant MCP-labelled REST routes only with a documented compatibility decision.
2. Replace process-local operation tracking with a durable `dbclaim` operation record, idempotency constraint, audited request service, and status query. MCP operation requests must enqueue/trigger only approved domain work and must never invoke worker internals directly. Verify recovery and status visibility after a process restart.
3. Complete OAuth hardening: use validated `OAuthOptions` access/refresh lifetimes, impose an explicit dynamic-registration admission policy and redirect-URI shape validation, persist consent records, stop retaining raw authorization codes, and revalidate registered client/redirect data on consent POST before redirecting. Add the required migrations and safe audit events.
4. Add an application-level rate-limit/throttle policy for MVC and OAuth/MCP-facing endpoints, with endpoint-appropriate limits, safe client identity keys, and denial behavior that does not expose sensitive information.
5. Update the OAuth threat model and interoperability/security test suite for the standard MCP transport, durable operation lifecycle, registration/redirect rejection, consent persistence, code confidentiality, configured lifetimes, throttling, PKCE, rotation, revocation, scope, ownership, and role boundaries.

## Done when

- A conforming MCP client can discover and invoke only the registered tools through the standard transport as its concrete OAuth user.
- MCP operations survive restart, are idempotent and auditable, and cannot bypass shared services or worker boundaries.
- OAuth redirect, consent, authorization-code, lifetime, and abuse-control paths have automated negative coverage and an updated threat model.

## Progress

| Item | Status | Updated | Scope, evidence, or blocker |
| --- | --- | --- | --- |
| 1 | In progress | 2026-09-03 | Jules / `jules-6784970465290902219-ad347328` — Register standard MCP tool services in DI and transport adapter endpoints. Affected areas: `Program.cs`, `DependencyInjection.cs`, `Mcp/Tools/`, controllers, and Web tests. |
| 2 | Not started | — | — |
| 3 | Not started | — | — |
| 4 | Not started | — | — |
| 5 | Not started | — | — |
