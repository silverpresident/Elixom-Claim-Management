# Architectural Decision Records (ADRs)

This directory contains Architectural Decision Records (ADRs) for the Elixom Claim Management system. Architectural decisions capture significant design, security, data governance, integration, and delivery choices.

## Guidelines

1. **Format:** Use [`template.md`](template.md) when proposing a new ADR.
2. **Naming Convention:** `XXXX-short-title.md` (e.g., `0001-in-house-oauth-server.md`).
3. **Immutability:** Do not edit past decisions to change history. Propose a new ADR that supersedes the previous decision.

## Decision Index

| Number | Title | Status | Date |
| --- | --- | --- | --- |
| Template | [ADR Template](template.md) | Standard | 2026-09-02 |
| 0001 | [In-House OAuth 2.0 Server with PKCE and Dynamic Client Registration](0001-in-house-oauth-server.md) | Accepted | 2026-09-02 |
| 0002 | [Reversal and Adjustment Accounting](0002-reversal-adjustment-accounting.md) | Accepted | 2026-09-02 |
| 0003 | [Audit Record Database Immutability](0003-audit-record-database-immutability.md) | Accepted | 2026-09-03 |
| 0004 | [All-Guid Identifier Convention Across Domain Entities](0004-all-guid-identifier-convention.md) | Accepted | 2026-09-03 |
