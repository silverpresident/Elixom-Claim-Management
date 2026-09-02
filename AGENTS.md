# Engineering Guide for Agents

## First actions

1. Read `README.md`, then the relevant file in `sprints/`.
2. Read `MEMORY.md` before making an architectural decision.
3. Inspect the existing code and tests; do not overwrite unrelated user changes.
4. Implement the smallest coherent vertical slice, test it, and update `MEMORY.md` if the project’s durable state changed.

## Non-negotiable architecture

- Target .NET 10, C# 14, ASP.NET Core MVC, EF Core, Azure SQL, and schema `dbclaim`.
- Keep business rules, entities, data access, and reusable services in `ElixomClaim.Lib`; keep HTTP/Razor/transport wiring in `ElixomClaim.Web`.
- Controllers, background workers, and MCP tools are thin adapters over shared services. Do not duplicate business decisions in an adapter.
- Use async I/O, UTC timestamps, `decimal(18,2)` money, transactions for aggregate state changes, and database constraints for invariants that must survive concurrent requests.
- Remove default `Class1.cs` files. Do not introduce a local Bootstrap or jQuery distribution.

## Security and privacy

- Web sessions use Google OpenID Connect. Only provisioned active users may enter, except the explicitly configured bootstrap administrator flow.
- MCP uses the built-in OAuth authorization server with authorization code + PKCE S256. MCP actions inherit the concrete user identity and never bypass authorization.
- Treat bank details, emails, OAuth tokens, connection strings, and client secrets as sensitive. Never put them in source, tests, exception text, audit payloads, or application logs.
- Apply authorization and ownership checks in shared services, then enforce them at endpoints as defense in depth.
- Log security events, mutations, OAuth events, and MCP operations through the audit service. Keep audit records append-only.

## Domain guardrails

- Claims are soft-deleted and hidden by default. A claimant cannot change an accepted/rejected claim.
- A collection must be `Collected` before attachment, and every collection in a job has the same client.
- A job belongs to either a user or a collection client, never both; only `Processing` jobs can change line items.
- Marking a job paid performs all related status updates atomically and queues its notification exactly once.
- Payroll is generated only by salary definitions. Generated entries are locked; custom negative entries must not make net pay negative.
- Receipts and notifications are HTML only. PDF generation is prohibited.

## Frontend and quality

- Use Bootstrap and jQuery from CDN links only, with SRI where available. Prefer accessible, semantic Razor markup and responsive/print styles.
- Include `ILogger<T>` in controllers, services, and hosted services. Use structured, redacted logs.
- Add/maintain unit tests for services and lifecycle rules and integration tests for authorization, endpoints, and critical persistence behavior.
- Run the relevant formatter, build, and tests before handoff. Report commands run and limitations honestly.

## MEMORY.md protocol

`MEMORY.md` is a concise ledger, not a task diary. Update it in the same change when any of these changes: architecture, schema/migration state, externally visible behavior, authorization/security posture, integrations/configuration contract, completed sprint, or a decision/risk needing future attention.

Each entry needs a date, concise fact/decision, affected area, and a link to the code, migration, test, issue, or sprint when available. Keep current facts in their named section and move superseded entries to the decision log; never paste secrets, token values, personal data, or verbose command output.
