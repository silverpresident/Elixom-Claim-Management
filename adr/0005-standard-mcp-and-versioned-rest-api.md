# Standard MCP Server Endpoint and Versioned REST API Contract

- **Status:** Accepted
- **Date:** 2026-09-03
- **Author:** Engineering

## Context

The system previously exposed bespoke ASP.NET Core MVC controllers under `/mcp/*` returning JSON responses. While named with an `Mcp` prefix, these endpoints did not implement the standard Model Context Protocol (MCP) HTTP transport or tool discovery protocol defined by `ModelContextProtocol.AspNetCore`. This created a transport protocol gap for conforming MCP client agents and left external non-MCP integrations without a clean, versioned REST API.

To establish clear operational boundaries, conform to official MCP standards, and provide robust integration contracts for both AI agents and standard client applications, a dual-transport strategy over shared domain services is required.

## Decision

The system establishes two distinct, deliberate HTTP transport interfaces sharing a unified authenticated actor boundary and Lib domain service layer:

1. **Standard MCP Server Endpoint (`/mcp`):**
   - The `/mcp` endpoint is strictly reserved for standard Model Context Protocol (MCP) HTTP transport powered by the official `ModelContextProtocol.AspNetCore` SDK (version 2.2.0).
   - Authorizes requests using Bearer token authentication requiring the explicit `mcp:access` OAuth scope.
   - Exposes domain-scoped tool groups (`ClaimTools`, `CollectionTools`, `JobPaymentTools`, `PayrollTools`, `EmailTools`, `OperationsTools`) via standard MCP tool discovery and tool invocation protocol methods.
   - Handles errors using standard MCP JSON-RPC protocol error payloads.

2. **Versioned REST API (`/api/v1`):**
   - A dedicated REST API surface is established under `/api/v1` for standard web, mobile, and third-party HTTP integrations.
   - Authorizes requests using Bearer token authentication requiring the explicit `api:access` OAuth scope.
   - Exposes RESTful resource endpoints (`/api/v1/claims`, `/api/v1/collections`, `/api/v1/job-payments`, `/api/v1/email-templates`, `/api/v1/payroll`, `/api/v1/operations`).
   - Uses HTTP status codes and RFC 7807 Problem Details (`application/problem+json`) for error responses.

3. **Retirement of Bespoke `/mcp/*` Controllers:**
   - All legacy bespoke `Mcp*Controller` classes and `/mcp/*` routes are retired and deleted.
   - No legacy `/mcp/*` controller compatibility layer or ambiguous lookalike routes are retained.

4. **OAuth Scope Separation:**
   - OAuth dynamic client registration and token issuance enforce strict scope isolation.
   - `mcp:access` grants access exclusively to the standard `/mcp` endpoint.
   - `api:access` grants access exclusively to `/api/v1` REST endpoints.
   - Cross-scope usage is rejected with `403 Forbidden`.

5. **Correlation & Idempotency:**
   - Both `/mcp` and `/api/v1` accept optional `X-Correlation-ID` headers (or auto-generate UUIDs when absent) and attach correlation IDs to audit records and response metadata.
   - Command endpoints support `Idempotency-Key` or `X-Idempotency-Key` headers backed by durable operation records in `dbclaim.OperationRecords`.

6. **Pagination & Filtering Conventions:**
   - Collection list endpoints support standard `page` (1-indexed, default 1) and `pageSize` (default 20, max 100) query parameters.

7. **Explicit Operational Exclusions:**
   - Neither MCP tools nor `/api/v1` REST endpoints expose:
     - Arbitrary database or SQL query execution or generic CRUD bypasses.
     - Direct invocation of hosted background worker internals.
     - Unmasked sensitive bank account numbers to non-Accountant/Administrator roles.
     - Free-form email sending or arbitrary external recipient composition (only approved system templates queued via durable outbox).
     - Dynamic PDF file generation (printable HTML/CSS documents only).

## Consequences

### Positive

- Ensures full compatibility with standard MCP client agents using the official SDK.
- Delivers a clean, versioned, documented REST API for non-MCP HTTP clients.
- Prevents cross-transport privilege escalation through strict OAuth scope separation (`mcp:access` vs `api:access`).
- Retains centralized business rule enforcement, ownership checks, and append-only audit logging in shared `ElixomClaim.Lib` services.

### Negative / Trade-offs

- Requires clients previously using bespoke `/mcp/*` endpoints to update their target URL to standard `/mcp` (for MCP JSON-RPC clients) or `/api/v1/*` (for REST clients) and request appropriate OAuth scopes.

## Security & Compliance

- Enforces defense-in-depth: OAuth token validation + transport scope check + shared service identity/role authorization.
- All requests write structured audit records capturing actor, action, transport classification (`IsMcp`), correlation ID, and redacted metadata.
