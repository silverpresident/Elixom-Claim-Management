# Release Readiness: Next Steps and Evidence Record

Use this document to coordinate the remaining Sprint 13 release controls. Complete the steps in order. Store completed evidence in the approved release repository or ticket system; do not place secrets, tokens, connection strings, bank data, unredacted email content, or personal data in this document.

## 1. Assign release ownership

- Name one release owner with go/no-go authority.
- Name one migration runner; this must be the only process permitted to apply the initial migration.
- Name an independent observer for the migration rehearsal.
- Create a release/change record and record its identifier below.

## 2. Prepare controlled staging

- Create the production-shaped Azure SQL staging database. It must be empty: the clean `20260910110439_InitialCreate` baseline is not an upgrade path for a database containing application data.
- Provision non-production Google OIDC test identities for User, Teller, Manager, Accountant, and Administrator.
- Configure a safe SMTP/ACS test account and controlled recipient mailbox. Do not use production delivery credentials or recipients.
- Configure staging secrets through the approved secret store, then verify HTTPS, the Google callback URI, and the single migration-runner deployment configuration.

## 3. Rehearse migration and recovery

Follow [the literal audit/email migration runbook](runbooks/literal-audit-email-migration.md) and [the backup/restore runbook](runbooks/backup-restore.md).

- Generate and checksum the reviewed idempotent EF script.
- Apply it once with exactly one migration runner; apply it again to prove idempotence.
- Verify `dbclaim`, literal audit/email fields, required indexes, and `TR_AuditRecords_PreventMutation`.
- Create only synthetic records and prove the trigger rejects audit updates/deletes.
- Take a backup/PITR recovery point, restore it to a new staging database, and verify the baseline and sample records.

## 4. Run staging smoke tests

Record pass/fail and an evidence reference for each:

| Area | Required result |
| --- | --- |
| Google sign-in | Each provisioned role signs in; Blocked/inactive user is denied. |
| OAuth | Authorization Code + PKCE S256, refresh rotation, revoked/expired tokens, consent, and redirect validation behave as documented. |
| API/MCP isolation | `api:access` and `mcp:access` remain isolated; MCP uses the concrete caller identity. |
| MCP interoperability | A conforming MCP client discovers and invokes the approved tool set. |
| Email delivery | Approved receipt and payout messages use configured `From`, Bcc-only system copy, safe recipients, durable retry, and no Bcc disclosure in recipient-visible content or previews. |
| Audit | Audit events are append-only; Manager and Administrator projections retain their approved scope. |
| Recovery/retention | Backup/restore succeeds; retention configuration remains at or above the four-year floor. |

## 5. Complete independent OAuth/MCP review

Use [the independent review runbook](runbooks/independent-oauth-security-review.md).

- Select an assessor independent of implementation.
- Provide the documented staging scope, test identities, threat model, API/MCP contract, and redacted automated-test evidence.
- Track every finding with severity, owner, remediation, retest outcome, and any time-bound residual-risk approval.
- Do not proceed with an unaccepted Critical or High finding.

## 6. Publish the release evidence record

Copy this template into the approved release ticket or evidence repository and fill it in. Attach or link evidence rather than pasting sensitive command output.

```markdown
# Elixom Claim release evidence — <release identifier>

## Accountability

| Field | Value |
| --- | --- |
| Release owner | |
| Migration runner | |
| Independent observer | |
| Change/release record | |
| Staging environment identifier | |
| Application build/commit | |
| Evidence period (UTC) | |

## Migration and recovery

| Check | Result | Evidence reference | Notes / safe counts only |
| --- | --- | --- | --- |
| Reviewed idempotent script checksum | Pass / Fail | | |
| Single-runner migration | Pass / Fail | | |
| Idempotent second execution | Pass / Fail | | |
| Literal audit/email schema and indexes | Pass / Fail | | |
| Append-only trigger update/delete rejection | Pass / Fail | | |
| Backup/PITR recovery | Pass / Fail | | |

## Staging smoke tests

| Check | Result | Evidence reference | Notes |
| --- | --- | --- | --- |
| Google OIDC role matrix | Pass / Fail | | |
| OAuth PKCE/refresh/revocation | Pass / Fail | | |
| API/MCP scope isolation and ownership | Pass / Fail | | |
| MCP interoperability and cancellation | Pass / Fail | | |
| Safe email/Bcc/retry evidence | Pass / Fail | | |
| Audit projections and immutability | Pass / Fail | | |
| Retention floor | Pass / Fail | | |

## Independent OAuth/MCP review

| Field | Value |
| --- | --- |
| Assessor and engagement reference | |
| Report version/location | |
| Critical findings and disposition | |
| High findings and disposition | |
| Retest outcome/date | |
| Residual risks, approver, and review date | |

## Go / no-go

| Decision | Approver | UTC date | Rationale / blockers |
| --- | --- | --- | --- |
| Go / No-go / Conditional go | | | |
```

## Release rule

The release owner records `Go` only when every required check passes, the independent review is complete, and no Critical or High finding remains unresolved or formally accepted by the accountable authority. Otherwise record `No-go`, identify the owner and remediation path, and schedule a new evidence review.
