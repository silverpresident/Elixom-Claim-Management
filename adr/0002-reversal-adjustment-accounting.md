# Reversal and Adjustment Accounting

- **Status:** Accepted
- **Date:** 2026-09-02
- **Author:** Product owner

## Context

Paid job payments are immutable financial records. The system needs a correction path that does not roll back paid claims, collections, or payrolls, while keeping the financial and authorization trail clear.

## Decision

Use separately linked, partial or full adjustment job payments. An Accountant creates an adjustment linked to the original paid job; an Administrator approves it; an Accountant settles it. Adjustments are accounting-only corrections and do not require bank recovery before settlement. Original source records remain paid and immutable. An adjustment may be positive or negative, must include a reason and original-payment link, and a negative net amount is recorded as a recovery receivable rather than a negative payout.

## Consequences

### Positive

- Preserves an immutable paid-payment history.
- Supports partial corrections without changing original line states.
- Separates preparation, approval, and settlement duties.

### Negative / Trade-offs

- Recovery receivables need operational follow-up outside the payout execution flow.
- The adjustment workflow adds approval state and audit requirements.

## Security & Compliance

All creation, approval, settlement, and notification events are append-only audited. The original payment linkage and adjustment reason are required. Notifications use the durable outbox and do not expose internal notes.
