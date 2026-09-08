# API and MCP Integration Specifications

## Overview

This specification establishes the contracts for two distinct HTTP transports supported by the application:
1. **Model Context Protocol (MCP) Server Endpoint** (`/mcp`) — For AI agents and tool-calling clients speaking the standard MCP protocol.
2. **Versioned REST API** (`/api/v1/*`) — For external software integrations, web clients, and third-party applications.

Both transports run on top of shared core services in `ElixomClaim.Lib` and enforce defense-in-depth authorization, concrete user identity, role boundaries, and append-only audit logging.

---

## Authentication & OAuth Scopes

Access to both `/mcp` and `/api/v1` requires a Bearer access token issued by the built-in OAuth 2.0 authorization server (`/oauth/authorize` and `/oauth/token`).

| Scope | Authorized Transport | Description |
| --- | --- | --- |
| `mcp:access` | `/mcp` | Grants access to the standard Model Context Protocol endpoint and tool execution. |
| `api:access` | `/api/v1/*` | Grants access to the versioned REST API resources. |
| `openid` | `/oauth/*` | Standard OpenID Connect identity scope. |
| `profile` | `/oauth/*` | Basic user profile information scope. |
| `email` | `/oauth/*` | User email scope. |

Tokens carrying only `mcp:access` will be rejected with `403 Forbidden` if presented to `/api/v1/*`. Conversely, tokens carrying only `api:access` will be rejected with `403 Forbidden` if presented to `/mcp`.

Dynamic registration at `/oauth/register` creates a **public** MCP client: it returns no client secret, requires PKCE S256, and is limited to `openid profile email mcp:access`. Its requested scopes are rejected unless every scope is present in that client's persisted allow-list. Confidential clients are trusted, administratively provisioned integrations; they must retain a hashed client secret and authenticate at token exchange and refresh. Existing pre-policy clients migrate as confidential clients.

---

## Headers & Global Conventions

### Request Headers

| Header | Required | Format / Values | Description |
| --- | --- | --- | --- |
| `Authorization` | Yes | `Bearer <access_token>` | OAuth 2.0 Bearer access token. |
| `X-Correlation-ID` | Optional | String (UUID recommended) | Correlation tracking ID. Auto-generated UUID if omitted. |
| `Idempotency-Key` / `X-Idempotency-Key` | Optional (Commands) | String (UUID recommended) | Unique key for operation idempotency and deduplication. |
| `Accept` | Optional | `application/json` or `application/problem+json` | Media type expectation. |

### Response Headers

| Header | Format / Values | Description |
| --- | --- | --- |
| `X-Correlation-ID` | String | Echoed or generated correlation tracking identifier. |
| `X-RateLimit-Limit` | Integer | Requests per minute limit for the endpoint policy. |
| `X-RateLimit-Remaining` | Integer | Remaining requests in the current window. |

---

## Rate Limiting

Endpoints are rate-limited using ASP.NET Core RateLimiting middleware:
- **OAuth (`/oauth/*`):** 20 requests/minute per client/IP.
- **MCP (`/mcp`):** 60 requests/minute per user/IP.
- **REST API (`/api/v1/*`):** 100 requests/minute per user/IP.

Exceeding rate limits returns `429 Too Many Requests` with Problem Details JSON.

---

## Error Envelope (REST API `/api/v1/*`)

All `/api/v1/*` error responses use RFC 7807 Problem Details (`application/problem+json`).

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "The requested claim is in accepted status and cannot be modified.",
  "instance": "/api/v1/claims/9b1deb4d-3b7d-4bad-9bdd-2b0d7b3d0123",
  "correlationId": "c8a3f124-7b89-4e5a-9012-3456789abcde",
  "errors": {
    "claim": [ "Claim is already finalized." ]
  }
}
```

---

## Model Context Protocol (`/mcp`) Tool Specifications

The `/mcp` endpoint uses the official `ModelContextProtocol.AspNetCore` HTTP server SDK. Tools are grouped into six domain-scoped tool classes:

### 1. `ClaimTools`
- `claims_list` — List claims accessible to the authenticated user.
- `claims_get` — Retrieve detailed claim information by Guid. Guid identifiers remain transport keys; `SequenceNo` is the human-facing record number returned/displayed for supported domain records.
- `claims_submit` — Submit a draft claim owned by the user.

### 2. `CollectionTools`
- `collections_list` — List collections (Teller+ required).
- `collections_get` — Retrieve collection information by Guid; payor email and internal processing fees are never returned.

### 3. `JobPaymentTools`
- `job_payments_list` — List job payment records (payee ownership or Manager+).
- `job_payments_get` — Retrieve job payment details (sensitive details Accountant+ only).

### 4. `EmailTools`
- `email_preview` — Redacted preview of an approved email template.
- `email_queue` — Request outbox delivery of an approved template email to pre-authorized recipients.

### 5. `PayrollTools`
- `payroll_preview` — Service-backed payroll preview for salary definitions (Accountant+).
- `payroll_run` — Generate salary-sourced payroll entry (Accountant+).

### 6. `OperationsTools`
- `operations_salary_generation` — Request authorized, idempotent salary generation (Accountant+).
- `operations_outbox_wakeup` — Request an authorized, idempotent outbox dispatch wake-up (Administrator only).
- `operations_status` — Query status of an approved operation.

---

## Versioned REST API (`/api/v1/*`) Resource Endpoints

The generated OpenAPI 3 document is available at `GET /openapi/v1.json` to an authenticated `api:access` caller. It is the machine-readable contract for this REST surface; `/mcp` is intentionally absent because it is a separate MCP protocol endpoint.

### Claims (`/api/v1/claims`)
- `GET /api/v1/claims` — List claims owned by or accessible to the user (`page`, `pageSize`, `status`).
- `GET /api/v1/claims/{id}` — Retrieve claim details by Guid.
- `POST /api/v1/claims` — Create a draft claim.
- `POST /api/v1/claims/{id}/submit` — Submit a draft claim.

### Collections (`/api/v1/collections`)
- `GET /api/v1/collections` — List collections (`page`, `pageSize`, `clientId`) [Teller+].
- `GET /api/v1/collections/{id}` — Retrieve collection details by Guid [Teller+].

### Job Payments (`/api/v1/job-payments`)
- `GET /api/v1/job-payments` — List job payment records (`page`, `pageSize`, `status`) [Payee owner or Manager+].
- `GET /api/v1/job-payments/{id}` — Retrieve job payment details by Guid [Sensitive details Accountant+].

### Email Templates (`/api/v1/email-templates`)
- `POST /api/v1/email-templates/preview` — Preview an approved email template (redacted body).
- `POST /api/v1/email-templates/queue` — Queue an approved template email to an associated recipient.

### Payroll (`/api/v1/payroll`)
- `POST /api/v1/payroll/preview` — Preview salary-definition due periods and totals [Accountant+].
- `POST /api/v1/payroll/run` — Generate payroll entries for due salary definitions [Accountant+].

### Operations (`/api/v1/operations`)
- `POST /api/v1/operations` — Create a durable operation request [Accountant/Admin].
- `GET /api/v1/operations/{id}` — Query durable operation record status and result.

---

## Excluded Capabilities

The following capabilities are explicitly non-goals and forbidden across both `/mcp` and `/api/v1/*`:
1. Direct arbitrary database or SQL query execution.
2. Generic CRUD endpoints bypassing domain service validation.
3. Direct execution or internal manipulation of background hosted workers.
4. Unmasked display of bank account details to non-Accountant/Administrator roles.
5. Free-form email content creation or arbitrary external recipient sending.
6. PDF binary file generation (printable HTML/CSS documents only).
