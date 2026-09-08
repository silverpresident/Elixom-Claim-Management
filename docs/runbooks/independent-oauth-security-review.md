# Independent OAuth and MCP Security Review

## Purpose

This is a mandatory pre-production release gate for Elixom Claim's in-house OAuth 2.0 authorization server and MCP/API security boundary. The repository threat model and automated tests are supporting evidence; they are not an independent security assessment.

## Required assessor

Engage an independent application-security firm or specialist with demonstrated OAuth 2.0/OIDC and API security-review experience. The assessor must not be part of the implementation team. Prefer experience assessing Authorization Code + PKCE, dynamic client registration, bearer-token APIs, and MCP or comparable delegated-agent integrations.

## Statement of work

The agreed assessment must cover, at minimum:

- Authorization Code flow with mandatory PKCE S256, including code interception, verifier validation, expiry, and replay.
- Dynamic client registration, client-type policy, persisted allowed scopes, consent, and exact redirect-URI validation.
- Access-token issuance, validation, lifetime, revocation, refresh-token rotation/family replay handling, and key/secret handling.
- Scope separation between `mcp:access` and `api:access`; a token for one transport must not authorize the other.
- Standard MCP HTTP transport authentication, tool discovery/invocation, concrete-user identity propagation, role and record ownership enforcement, and safe cancellation/error behavior.
- API and MCP rate limiting, audit attribution, correlation, sensitive-data redaction, and safe error responses.
- Authenticated black-box testing against staging and source-assisted review of the relevant OAuth, bearer-authentication, actor-resolution, and MCP/API adapter code.
- Interoperability testing with a conforming MCP client.

The report must include severity-rated findings, reproducible evidence, affected endpoints/components, remediation guidance, and a retest result for each resolved finding.

## Staging and rules of engagement

Provide an isolated staging environment with production-equivalent security settings, HTTPS, disposable test users, and non-production data. Do not provide production credentials, tokens, bank details, email bodies, connection strings, or customer information.

Before testing, agree in writing:

- Authorized hosts, paths, test accounts, time window, rate/availability limits, and prohibited techniques.
- Contacts for assessment operations, incident escalation, and a stop-test request.
- Whether authenticated testing, source access, dependency scanning, and social-engineering testing are in scope. Social engineering is out of scope unless separately authorized.
- How test data, screenshots, traffic captures, and the final report will be stored and retained.

## Evidence package

Supply the assessor with:

- [OAuth threat model](../oauth-threat-model.md)
- [OAuth architecture decision](../../adr/0001-in-house-oauth-server.md)
- [API and MCP contract](../api-and-mcp-contract.md)
- Relevant automated test results and current release verification evidence
- OAuth key-rotation, incident-response, audit-review, backup/restore, and migration runbooks
- A test-account matrix covering User, Teller, Manager, Accountant, and Administrator roles

## Remediation and release decision

1. Record every finding as a tracked remediation item with owner, severity, affected release, and target date.
2. Fix findings in focused changes with automated regression coverage.
3. Have the assessor retest each resolved finding.
4. Record any accepted residual risk with an accountable business/security owner, expiry/review date, and rationale.
5. Do not release until critical and high findings are resolved or formally accepted according to the organization’s risk policy, all agreed retests are complete, and the final report is approved.

## Completion record

Record the following in Sprint 12 and `MEMORY.md` when the review is complete. Do not record secrets, token values, personal data, or unredacted evidence.

| Field | Record |
| --- | --- |
| Assessor and engagement reference | |
| Staging target and assessment dates | |
| Scope and exclusions | |
| Report version/location | |
| Critical/high findings and disposition | |
| Retest date and outcome | |
| Residual-risk approvals | |
| Release approver and date | |
