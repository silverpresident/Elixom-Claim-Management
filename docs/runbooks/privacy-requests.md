# Privacy Requests & Data Retention Management Runbook

## Overview
Elixom Claim operates under the privacy laws of Jamaica. The official privacy contact is `privacy@elixom.com`.

## Data Retention Guardrails
- **Financial & Audit Records Retention:** Retention is configured via `Retention:FinancialRecordRetentionYears` (default: 9 years).
- **Mandatory Retention Floor:** Configuration validation enforces a non-negotiable minimum retention floor of 4 years. Attempts to configure less than 4 years fail application startup.

## Handling Subject Access Requests (SAR)
1. Receive and verify identity of the data subject via `privacy@elixom.com`.
2. Extract user profile, claims, collections, job payments, and audit logs:
   ```sql
   SELECT * FROM dbclaim.Users WHERE Email = @UserEmail;
   SELECT * FROM dbclaim.Claims WHERE ClaimantUserId = @UserId;
   SELECT * FROM dbclaim.JobPayments WHERE PayeeUserId = @UserId;
   ```
3. Format output into readable PDF/HTML summary and transmit securely to user.

## Handling Right to Erasure / Anonymization Requests
1. Financial and audit records must be retained for the mandatory 9-year statutory retention period.
2. Active user account can be set to `Blocked` / `IsActive = false`.
3. Personal contact details (phone, full name) can be redacted/anonymized while preserving financial line item integrity.
